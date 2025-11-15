// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Helper StateMachineBehaviour that allows us to more easily play a specific weapon sound.
    /// </summary>
    public class PlaySoundCharacterBehaviour : StateMachineBehaviour
    {
        /// <summary>
        /// Type of weapon sound.
        /// </summary>
        private enum SoundType
        {
            //Holsters.
            Holster, Unholster,
            //Normal Reloads.
            Reload, ReloadEmpty,
            //Firing.
            Fire, FireEmpty,
        }

        #region FIELDS SERIALIZED

        [Header("Setup")]
        
        [Tooltip("Delay at which the audio is played.")]
        [SerializeField]
        private float delay;
        
        [Tooltip("Type of weapon sound to play.")]
        [SerializeField]
        private SoundType soundType;
        
        [Header("Audio Settings")]

        [Tooltip("Audio Settings.")]
        [SerializeField]
        private AudioSettings audioSettings = new AudioSettings(1.0f, 0.0f, true);

        #endregion

        #region FIELDS

        /// <summary>
        /// Player Character.
        /// </summary>
        // ALTERAÇÃO: Usa Character (a classe adaptada) em vez de CharacterBehaviour
        private Character playerCharacter; 

        /// <summary>
        /// Player Inventory.
        /// </summary>
        private InventoryBehaviour playerInventory;

        /// <summary>
        /// The service that handles sounds.
        /// </summary>
        private IAudioManagerService audioManagerService; // Mantemos o campo, mas não o usamos
        // private IAudioManagerService audioManagerService; // Original, mantemos a linha

        #endregion
        
        #region UNITY

        /// <summary>
        /// On State Enter.
        /// </summary>
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // CORREÇÃO: SUBSTITUIÇÃO DO SERVICE LOCATOR
            // Procura o Character no objeto principal do Animator (Ancestor)
            if (playerCharacter == null)
            {
                playerCharacter = animator.GetComponentInParent<Character>();
            }

            // ADIÇÃO CRUCIAL: VERIFICAÇÃO DE AUTORIDADE
            // Só reproduz o som se o Character existir e for o Owner (para o som ser local).
            if (playerCharacter == null || !playerCharacter.IsOwner)
                return;

            //Get Inventory.
            playerInventory ??= playerCharacter.GetInventory();

            //Try to get the equipped weapon's Weapon component.
            // ADIÇÃO DE CHECK NULO: O Inventory pode ser nulo se a inicialização falhou
            if (playerInventory == null || !(playerInventory.GetEquipped() is { } weaponBehaviour))
                return;
            
            // REMOVIDO: Não podemos usar Service Locator para o áudio
            // audioManagerService ??= ServiceLocator.Current.Get<IAudioManagerService>();

            // Tenta obter o AudioSource no objeto Character (root)
            if (!playerCharacter.TryGetComponent<AudioSource>(out var audioSource))
            {
                 // Se não houver AudioSource no root, não pode reproduzir o som
                 return;
            }


            #region Select Correct Clip To Play

            //Switch.
            AudioClip clip = soundType switch
            {
                //Holster.
                SoundType.Holster => weaponBehaviour.GetAudioClipHolster(),
                //Unholster.
                SoundType.Unholster => weaponBehaviour.GetAudioClipUnholster(),
                
                //Reload.
                SoundType.Reload => weaponBehaviour.GetAudioClipReload(),
                //Reload Empty.
                SoundType.ReloadEmpty => weaponBehaviour.GetAudioClipReloadEmpty(),
                
                //Fire.
                SoundType.Fire => weaponBehaviour.GetAudioClipFire(),
                //Fire Empty.
                SoundType.FireEmpty => weaponBehaviour.GetAudioClipFireEmpty(),
                
                //Default.
                _ => default
            };

            #endregion

            // NOVO CÓDIGO: Usa o AudioSource local que obtivemos
            if (clip != null)
            {
                // Tenta obter o volume a partir do valor pré-serializado (private) se possível, senão usa 1.0f.
                // Como não podemos aceder ao campo 'volume', usamos a solução do volume padrão.
                float finalVolume = 1.0f; 
    
                // Play with some delay.
                if (delay > 0.001f)
                    audioSource.PlayDelayed(delay);
                else
                    // CORREÇÃO: Substituímos o acesso ao campo privado (audioSettings.volume) pelo valor padrão de volume 1.0f,
                    // ou 0.0f se o audioSettings.volume fosse 0.0f (mas 1.0f é mais lógico para um som).
                    audioSource.PlayOneShot(clip, finalVolume); 
            }
            // LINHA ORIGINAL COMENTADA/REMOVIDA:
            // audioManagerService.PlayOneShotDelayed(clip, audioSettings, delay);
        }
        
        #endregion
    }
}