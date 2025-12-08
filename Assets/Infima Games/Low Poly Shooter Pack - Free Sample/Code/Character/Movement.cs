// Copyright 2021, Infima Games. All Rights Reserved.

using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem; // <-- para o salto via Input System

namespace InfimaGames.LowPolyShooterPack
{
    // ALTERAÇÃO: Removida herança MovementBehaviour
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class Movement : MonoBehaviour // Mudança para MonoBehaviour
    {
        #region FIELDS SERIALIZED
        
        [Header("Audio Clips")]
        [Tooltip("The audio clip that is played while walking.")]
        [SerializeField] private AudioClip audioClipWalking;

        [Tooltip("The audio clip that is played while running.")]
        [SerializeField] private AudioClip audioClipRunning;

        [Header("Speeds")]
        [SerializeField] private float speedWalking = 5.0f;

        [Tooltip("How fast the player moves while running.")]
        [SerializeField] private float speedRunning = 9.0f;

        [Header("Jump")]
        [Tooltip("Força do salto (em unidades/segundo aplicada como mudança instantânea de velocidade).")]
        [SerializeField] private float jumpVelocity = 5.5f;

        [Tooltip("Tempo mínimo entre saltos, em segundos.")]
        [SerializeField] private float jumpCooldown = 0.1f;

        [Tooltip("Input Action do salto (por ex., bound à tecla Space).")]
        [SerializeField] private InputActionReference jumpAction;
        
        [Header("Network Ref")] 
        [Tooltip("Referência ao script Character (Controller de Rede).")]
        public Character characterNetcode;

        [Tooltip("Referência ao script que gere a morte/respawn.")]
        [SerializeField] private PlayerDeathAndRespawn deathStateController;

        #endregion

        #region PROPERTIES

        private Vector3 Velocity
        {
            get => rigidBody.linearVelocity;
            set => rigidBody.linearVelocity = value;
        }

        #endregion

        #region FIELDS

        private Rigidbody rigidBody;
        private CapsuleCollider capsule;
        private AudioSource audioSource;

        private bool grounded;

        private Character playerCharacter;

        private WeaponBehaviour equippedWeapon;

        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private float nextJumpTime;
        
        private PlayerDeathAndRespawn deathState;

        // MELHORIA: valores para rampas e escadas
        private float maxSlopeAngle = 55f;
        private float stepHeight = 0.3f;
        private float stepSmooth = 0.1f;

        #endregion

        #region UNITY FUNCTIONS

        protected void Awake()
        {
            if (characterNetcode == null)
                characterNetcode = GetComponent<Character>();

            if (deathStateController == null)
                deathStateController = GetComponent<PlayerDeathAndRespawn>();
            
            playerCharacter = characterNetcode;
            deathState = deathStateController;

            if (playerCharacter == null)
                Debug.LogError("Movement: O script 'Character' (Controller de Rede) não foi encontrado.");

            if (jumpAction != null)
            {
                if (!jumpAction.action.enabled)
                    jumpAction.action.Enable();

                jumpAction.action.performed += OnJumpPerformed;
            }
        }

        protected void OnDestroy()
        {
            if (jumpAction != null)
                jumpAction.action.performed -= OnJumpPerformed;
        }

        protected void Start()
        {
            rigidBody = GetComponent<Rigidbody>();
            if (rigidBody != null)
            {
                rigidBody.useGravity = true;
                rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            capsule = GetComponent<CapsuleCollider>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.clip = audioClipWalking;
                audioSource.loop = true;
            }
        }

        private void OnCollisionStay()
        {
            if (capsule == null) return;

            Bounds bounds = capsule.bounds;
            Vector3 extents = bounds.extents;
            float radius = extents.x - 0.01f;

            Physics.SphereCastNonAlloc(bounds.center, radius, Vector3.down,
                groundHits, extents.y - radius * 0.5f, ~0, QueryTriggerInteraction.Ignore);

            if (groundHits.Any(hit => hit.collider != null && hit.collider != capsule))
            {
                grounded = true;
            }

            for (var i = 0; i < groundHits.Length; i++)
                groundHits[i] = new RaycastHit();
        }

        protected void FixedUpdate()
        {
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return;
            
            if (!CanMove())
            {
                if (rigidBody != null)
                {
                    Vector3 v = rigidBody.linearVelocity;
                    rigidBody.linearVelocity = new Vector3(0f, v.y, 0f);
                }
                return;
            }

            MoveCharacter();
            LedgeAssist();

            grounded = false;

            float gravityMultiplier = 2.0f;
            if (!grounded)
            {
                rigidBody.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        protected void Update()
        {
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return;

            equippedWeapon = playerCharacter.GetInventory()?.GetEquipped();

            PlayFootstepSounds();
        }

        #endregion

        #region METHODS - MOVEMENT & JUMP

        
        // MELHORIA: assistência para subir caixas mesmo durante o salto
        private void LedgeAssist()
        {
            if (grounded) return; // só interessa no ar

            float checkDistance = 0.4f;
            float ledgeHeight = 0.6f;
            float climbForce = 6.0f;

            // cast frontal para ver se estamos a bater numa caixa ou aresta
            Vector3 forwardOrigin = transform.position + Vector3.up * (capsule.height * 0.5f);
            if (Physics.Raycast(forwardOrigin, transform.forward, out RaycastHit hitFront, checkDistance))
            {
                // agora verificar se existe topo logo acima
                Vector3 topCheckOrigin = hitFront.point + Vector3.up * ledgeHeight;

                if (!Physics.Raycast(topCheckOrigin, Vector3.down, out RaycastHit topHit, 1.0f))
                    return;

                // aplica subida suave
                Vector3 newPos = rigidBody.position;
                newPos.y += Time.fixedDeltaTime * climbForce;
                rigidBody.position = newPos;
            }
        }
        
        
        private void MoveCharacter()
        {
            if (playerCharacter == null || rigidBody == null) return;

            Vector2 frameInput = playerCharacter.GetInputMovement();
            var movement = new Vector3(frameInput.x, 0.0f, frameInput.y);

            movement *= playerCharacter.IsRunning() ? speedRunning : speedWalking;
            movement = transform.TransformDirection(movement);

            // MELHORIA: assistência de rampas
            if (grounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 1.2f))
            {
                float slopeAngle = Vector3.Angle(slopeHit.normal, Vector3.up);

                if (slopeAngle < maxSlopeAngle)
                {
                    movement = Vector3.ProjectOnPlane(movement, slopeHit.normal);
                }
            }

            // MELHORIA: step climbing (não ficar preso em escadas)
            StepClimb();

            float currentY = rigidBody.linearVelocity.y;

            // MELHORIA: mais estável no chão
            if (grounded)
                rigidBody.MovePosition(rigidBody.position + movement * Time.fixedDeltaTime);
            else
                Velocity = new Vector3(movement.x, currentY, movement.z);
        }

        // MELHORIA - Step Offset funcional
        private void StepClimb()
        {
            Vector3 origin = transform.position + Vector3.up * (stepHeight + 0.1f);

            if (Physics.Raycast(origin, transform.forward, 0.3f))
            {
                if (!Physics.Raycast(transform.position + Vector3.up * 0.05f, transform.forward, 0.3f))
                {
                    Vector3 targetPos = transform.position + Vector3.up * stepSmooth;
                    rigidBody.position = Vector3.Lerp(rigidBody.position, targetPos, 0.5f);
                }
            }
        }

        private bool CanMove()
        {
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return false;

            if (deathState != null)
                return deathState.IsPlayerControlled;
            
            return true;
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            if (!CanMove()) return;
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return;

            TryJump();
        }

        private void TryJump()
        {
            if (rigidBody == null) return;

            if (Time.time < nextJumpTime)
                return;

            if (!CanMove()) return;
            if (!grounded) return;

            var v = rigidBody.linearVelocity;
            v.y = 0f;
            rigidBody.linearVelocity = v;

            rigidBody.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);

            nextJumpTime = Time.time + jumpCooldown;
            grounded = false;
        }

        #endregion

        #region METHODS - AUDIO

        private void PlayFootstepSounds()
        {
            if (audioSource == null || rigidBody == null || playerCharacter == null)
                return;
            
            if (!CanMove())
            {
                if (audioSource != null && audioSource.isPlaying)
                    audioSource.Pause();
                return;
            }

            Vector3 horizontalVel = rigidBody.linearVelocity; 
            horizontalVel.y = 0f;

            if (grounded && horizontalVel.sqrMagnitude > 0.1f)
            {
                audioSource.clip = playerCharacter.IsRunning() ? audioClipRunning : audioClipWalking;
                if (!audioSource.isPlaying)
                    audioSource.Play();
            }
            else if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }

        #endregion
    }
}
