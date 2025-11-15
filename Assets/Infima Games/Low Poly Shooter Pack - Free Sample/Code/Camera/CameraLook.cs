// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Camera Look. Handles the rotation of the camera.
    /// </summary>
    // ALTERAÇÃO: Removida herança desnecessária (já herda implicitamente de MonoBehaviour)
    public class CameraLook : MonoBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Network Ref")] // ADIÇÃO
        [Tooltip("Referência ao script Character (Controller de Rede).")]
        public Character characterNetcode; // ADIÇÃO
        
        [Header("Settings")]
        
        [Tooltip("Sensitivity when looking around.")]
        [SerializeField]
        private Vector2 sensitivity = new Vector2(1, 1);

        [Tooltip("Minimum and maximum up/down rotation angle the camera can have.")]
        [SerializeField]
        private Vector2 yClamp = new Vector2(-60, 60);

        [Tooltip("Should the look rotation be interpolated?")]
        [SerializeField]
        private bool smooth;

        [Tooltip("The speed at which the look rotation is interpolated.")]
        [SerializeField]
        private float interpolationSpeed = 25.0f;
        
        #endregion
        
        #region FIELDS
        
        /// <summary>
        /// Player Character.
        /// </summary>
        // ALTERAÇÃO: Agora armazena a nossa classe Character adaptada
        private Character playerCharacter;
        /// <summary>
        /// The player character's rigidbody component.
        /// </summary>
        private Rigidbody playerCharacterRigidbody;

        /// <summary>
        /// The player character's rotation.
        /// </summary>
        private Quaternion rotationCharacter;
        /// <summary>
        /// The camera's rotation.
        /// </summary>
        private Quaternion rotationCamera;

        #endregion
        
        #region UNITY

        // CORREÇÃO: Removido 'private' para 'protected' e removido 'override' (Se o original usasse)
        protected void Awake()
        {
            // ALTERAÇÃO CRUCIAL: Substitui Service Locator pela obtenção de componente
            if (characterNetcode == null)
            {
                // Tenta obter o Character no objeto raiz (pai)
                characterNetcode = GetComponentInParent<Character>(); 
            }
            playerCharacter = characterNetcode;
            
            if(playerCharacter == null)
            {
                Debug.LogError("CameraLook: O script 'Character' (Controller de Rede) não foi encontrado.");
                // Retorna, mas não interrompe, para dar chance aos outros scripts (mas o código de Start/LateUpdate vai ter que lidar com isto)
                return;
            }

            //Cache the rigidbody.
            playerCharacterRigidbody = playerCharacter.GetComponent<Rigidbody>();
        }
        
        protected void Start()
        {
            if (playerCharacterRigidbody == null)
            {
                 Debug.LogError("CameraLook: Rigidbody não encontrado no objeto principal do Player.");
                 return;
            }
            
            //Cache the character's initial rotation.
            rotationCharacter = playerCharacter.transform.localRotation;
            //Cache the camera's initial rotation.
            rotationCamera = transform.localRotation;
        }
        
        protected void LateUpdate()
        {
            // ADIÇÃO DE CHECK DE SEGURANÇA: Garantir que o script está ligado e é o dono
            // O Character.cs deve desligar este script para remotos, mas esta verificação é extra
            if (playerCharacter == null || !playerCharacter.isActiveAndEnabled || !playerCharacter.IsOwner) return;

            //Frame Input. The Input to add this frame!
            Vector2 frameInput = playerCharacter.IsCursorLocked() ? playerCharacter.GetInputLook() : default;
            //Sensitivity.
            frameInput *= sensitivity;

            //Yaw.
            Quaternion rotationYaw = Quaternion.Euler(0.0f, frameInput.x, 0.0f);
            //Pitch.
            Quaternion rotationPitch = Quaternion.Euler(-frameInput.y, 0.0f, 0.0f);
            
            //Save rotation. We use this for smooth rotation.
            rotationCamera *= rotationPitch;
            rotationCharacter *= rotationYaw;
            
            //Local Rotation.
            Quaternion localRotation = transform.localRotation;

            //Smooth.
            if (smooth)
            {
                //Interpolate local rotation.
                localRotation = Quaternion.Slerp(localRotation, rotationCamera, Time.deltaTime * interpolationSpeed);
                //Interpolate character rotation.
                playerCharacterRigidbody.MoveRotation(Quaternion.Slerp(playerCharacterRigidbody.rotation, rotationCharacter, Time.deltaTime * interpolationSpeed));
            }
            else
            {
                //Rotate local.
                localRotation *= rotationPitch;
                //Clamp.
                localRotation = Clamp(localRotation);

                //Rotate character.
                playerCharacterRigidbody.MoveRotation(playerCharacterRigidbody.rotation * rotationYaw);
            }
            
            //Set.
            transform.localRotation = localRotation;
        }

        #endregion

        #region FUNCTIONS

        /// <summary>
        /// Clamps the pitch of a quaternion according to our clamps.
        /// </summary>
        private Quaternion Clamp(Quaternion rotation)
        {
            rotation.x /= rotation.w;
            rotation.y /= rotation.w;
            rotation.z /= rotation.w;
            rotation.w = 1.0f;

            //Pitch.
            float pitch = 2.0f * Mathf.Rad2Deg * Mathf.Atan(rotation.x);

            //Clamp.
            pitch = Mathf.Clamp(pitch, yClamp.x, yClamp.y);
            rotation.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * pitch);

            //Return.
            return rotation;
        }

        #endregion
    }
}