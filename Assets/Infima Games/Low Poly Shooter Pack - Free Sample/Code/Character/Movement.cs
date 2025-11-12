// Copyright 2021, Infima Games. All Rights Reserved.

using System.Linq;
using UnityEngine;

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
        [SerializeField]
        private AudioClip audioClipWalking;

        [Tooltip("The audio clip that is played while running.")]
        [SerializeField]
        private AudioClip audioClipRunning;

        [Header("Speeds")]

        [SerializeField]
        private float speedWalking = 5.0f;

        [Tooltip("How fast the player moves while running."), SerializeField]
        private float speedRunning = 9.0f;

        #endregion

        #region PROPERTIES

        //Velocity.
        private Vector3 Velocity
        {
            //Getter.
            get => rigidBody.linearVelocity;
            //Setter.
            set => rigidBody.linearVelocity = value;
        }

        #endregion

        #region FIELDS

        /// <summary>
        /// Attached Rigidbody.
        /// </summary>
        private Rigidbody rigidBody;
        /// <summary>
        /// Attached CapsuleCollider.
        /// </summary>
        private CapsuleCollider capsule;
        /// <summary>
        /// Attached AudioSource.
        /// </summary>
        private AudioSource audioSource;
        
        /// <summary>
        /// True if the character is currently grounded.
        /// </summary>
        private bool grounded;

        /// <summary>
        /// Player Character.
        /// </summary>
        // ALTERAÇÃO: Agora armazena a nossa classe Character adaptada
        private Character playerCharacter; 
        /// <summary>
        /// The player character's equipped weapon.
        /// </summary>
        private WeaponBehaviour equippedWeapon;
        
        /// <summary>
        /// Array of RaycastHits used for ground checking.
        /// </summary>
        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        #endregion

        #region UNITY FUNCTIONS

        /// <summary>
        /// Awake.
        /// </summary>
        protected void Awake() // Removido 'override'
        {
            // ALTERAÇÃO CRUCIAL: Substitui Service Locator pela obtenção de componente
            if (characterNetcode == null)
            {
                // Tenta obter o script Character no próprio objeto (mais robusto)
                characterNetcode = GetComponent<Character>();
            }
            // ALTERAÇÃO: Atribui a referência
            playerCharacter = characterNetcode;
            
            if(playerCharacter == null)
            {
                // Deixa de ser um erro bloqueante para dar chance aos outros scripts
                Debug.LogError("Movement: O script 'Character' (Controller de Rede) não foi encontrado.");
            }
        }

        /// Initializes the FpsController on start.
        protected void Start() // Removido 'override'
        {
            //Rigidbody Setup.
            rigidBody = GetComponent<Rigidbody>();
            if (rigidBody) rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
            //Cache the CapsuleCollider.
            capsule = GetComponent<CapsuleCollider>();

            //Audio Source Setup.
            audioSource = GetComponent<AudioSource>();
            audioSource.clip = audioClipWalking;
            audioSource.loop = true;
        }

        /// Checks if the character is on the ground.
        private void OnCollisionStay()
        {
            //Bounds.
            Bounds bounds = capsule.bounds;
            //Extents.
            Vector3 extents = bounds.extents;
            //Radius.
            float radius = extents.x - 0.01f;
            
            //Cast. This checks whether there is indeed ground, or not.
            Physics.SphereCastNonAlloc(bounds.center, radius, Vector3.down,
                groundHits, extents.y - radius * 0.5f, ~0, QueryTriggerInteraction.Ignore);
            
            //We can ignore the rest if we don't have any proper hits.
            if (!groundHits.Any(hit => hit.collider != null && hit.collider != capsule)) 
                return;
            
            //Store RaycastHits.
            for (var i = 0; i < groundHits.Length; i++)
                groundHits[i] = new RaycastHit();

            //Set grounded. Now we know for sure that we're grounded.
            grounded = true;
        }
          
        protected void FixedUpdate() // Removido 'override'
        {
            // ADIÇÃO CRUCIAL: Só move o Rigidbody se for o Owner
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner) return;
            
            //Move.
            MoveCharacter();
            
            //Unground.
            grounded = false;
        }

        /// Moves the camera to the character, processes jumping and plays sounds every frame.
        protected void Update() // Removido 'override'
        {
            // ADIÇÃO CRUCIAL: Só atualiza o som se for o Owner
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner) return;
            
            //Get the equipped weapon!
            equippedWeapon = playerCharacter.GetInventory().GetEquipped();
            
            //Play Sounds!
            PlayFootstepSounds();
        }

        #endregion

        #region METHODS

        private void MoveCharacter()
        {
            #region Calculate Movement Velocity

            //Get Movement Input!
            // ALTERAÇÃO: playerCharacter é agora a nossa classe Character adaptada
            Vector2 frameInput = playerCharacter.GetInputMovement(); 
            //Calculate local-space direction by using the player's input.
            var movement = new Vector3(frameInput.x, 0.0f, frameInput.y);
            
            //Running speed calculation.
            if(playerCharacter.IsRunning())
                movement *= speedRunning;
            else
            {
                //Multiply by the normal walking speed.
                movement *= speedWalking;
            }

            //World space velocity calculation. This allows us to add it to the rigidbody's velocity properly.
            movement = transform.TransformDirection(movement);

            #endregion
            
            //Update Velocity.
            Velocity = new Vector3(movement.x, 0.0f, movement.z);
        }

        /// <summary>
        /// Plays Footstep Sounds. This code is slightly old, so may not be great, but it functions alright-y!
        /// </summary>
        private void PlayFootstepSounds()
        {
            //Check if we're moving on the ground. We don't need footsteps in the air.
            if (grounded && rigidBody != null && rigidBody.linearVelocity.sqrMagnitude > 0.1f)
            {
                //Select the correct audio clip to play.
                audioSource.clip = playerCharacter.IsRunning() ? audioClipRunning : audioClipWalking;
                //Play it!
                if (audioSource != null && !audioSource.isPlaying)
                    audioSource.Play();
            }
            //Pause it if we're doing something like flying, or not moving!
            else if (audioSource != null && audioSource.isPlaying)
                audioSource.Pause();
        }

        #endregion
    }
}