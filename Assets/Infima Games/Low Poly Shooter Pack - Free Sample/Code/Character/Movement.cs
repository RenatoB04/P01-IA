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

        [Header("Network Ref")] // ADIÇÃO
        [Tooltip("Referência ao script Character (Controller de Rede).")]
        public Character characterNetcode; // ADIÇÃO
        
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

        #endregion

        #region PROPERTIES

        // Velocity (Unity 6 usa linearVelocity).
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

        /// <summary>True se o character está no chão.</summary>
        private bool grounded;

        /// <summary>Character (controlador principal).</summary>
        private Character playerCharacter;

        private WeaponBehaviour equippedWeapon;

        /// <summary>Raycasts para ground check.</summary>
        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        // Controle de cooldown do salto
        private float nextJumpTime;

        #endregion

        #region UNITY FUNCTIONS

        protected void Awake()
        {
            // Obter Character localmente se não foi ligado no Inspector
            if (characterNetcode == null)
                characterNetcode = GetComponent<Character>();

            playerCharacter = characterNetcode;

            if (playerCharacter == null)
                Debug.LogError("Movement: O script 'Character' (Controller de Rede) não foi encontrado.");

            // Ligar evento do salto (Input System)
            if (jumpAction != null)
            {
                // Garantir que a action está criada/activada
                if (!jumpAction.action.enabled)
                    jumpAction.action.Enable();

                jumpAction.action.performed += OnJumpPerformed;
            }
        }

        protected void OnDestroy()
        {
            // Desligar evento do salto
            if (jumpAction != null)
                jumpAction.action.performed -= OnJumpPerformed;
        }

        protected void Start()
        {
            rigidBody = GetComponent<Rigidbody>();
            if (rigidBody != null)
            {
                rigidBody.useGravity = true;                  // garantir gravidade do Rigidbody
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

        /// <summary>
        /// Ground check simples por contacto (mantém grounded enquanto houver colisão abaixo).
        /// </summary>
        private void OnCollisionStay()
        {
            if (capsule == null) return;

            Bounds bounds = capsule.bounds;
            Vector3 extents = bounds.extents;
            float radius = extents.x - 0.01f;

            // Cast para baixo a partir do centro do capsule
            Physics.SphereCastNonAlloc(bounds.center, radius, Vector3.down,
                groundHits, extents.y - radius * 0.5f, ~0, QueryTriggerInteraction.Ignore);

            // grounded se bater em algo que não seja o nosso próprio collider
            if (groundHits.Any(hit => hit.collider != null && hit.collider != capsule))
            {
                grounded = true;
            }

            // limpar hits
            for (var i = 0; i < groundHits.Length; i++)
                groundHits[i] = new RaycastHit();
        }

        protected void FixedUpdate()
        {
            // Só o owner controla o movimento
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return;

            MoveCharacter();

            // Libertar grounded; será reposto em OnCollisionStay quando tocar no chão
            grounded = false;
            
            float gravityMultiplier = 2.0f; // aumenta ou diminui conforme o gosto
            if (!grounded)
            {
                // aplica gravidade extra apenas enquanto está no ar
                rigidBody.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        protected void Update()
        {
            // Só o owner trata de áudio
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return;

            // HUD/sons
            equippedWeapon = playerCharacter.GetInventory()?.GetEquipped();

            PlayFootstepSounds();
        }

        #endregion

        #region METHODS - MOVEMENT & JUMP

        private void MoveCharacter()
        {
            if (playerCharacter == null || rigidBody == null) return;

            // Input 2D (x,z) vindo do Character
            Vector2 frameInput = playerCharacter.GetInputMovement();
            var movement = new Vector3(frameInput.x, 0.0f, frameInput.y);

            // Velocidade (walk/run)
            movement *= playerCharacter.IsRunning() ? speedRunning : speedWalking;

            // Para o espaço do mundo, com base na orientação do player
            movement = transform.TransformDirection(movement);

            // PRESERVAR VELOCIDADE VERTICAL!
            float currentY = rigidBody.linearVelocity.y;

            // Aplicar velocidade de movimento só em XZ
            Velocity = new Vector3(movement.x, currentY, movement.z);
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            // Só processa no owner
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner)
                return;

            TryJump();
        }

        private void TryJump()
        {
            if (rigidBody == null) return;

            // cooldown simples
            if (Time.time < nextJumpTime)
                return;

            // só salta se grounded
            if (!grounded)
                return;

            // reset vertical para salto previsível
            var v = rigidBody.linearVelocity;
            v.y = 0f;
            rigidBody.linearVelocity = v;

            // aplicar "impulso" vertical (VelocityChange = ignora massa)
            rigidBody.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);

            // marcar cooldown e sair do chão
            nextJumpTime = Time.time + jumpCooldown;
            grounded = false;
        }

        #endregion

        #region METHODS - AUDIO

        private void PlayFootstepSounds()
        {
            if (audioSource == null || rigidBody == null || playerCharacter == null)
                return;

            // passos só quando está no chão e com velocidade horizontal
            Vector3 horizontalVel = rigidBody.linearVelocity; horizontalVel.y = 0f;
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
