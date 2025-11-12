// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using System.Collections;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Muzzle.
    /// </summary>
    public class Muzzle : MuzzleBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]
        
        [Tooltip("Socket at the tip of the Muzzle. Commonly used as a firing point.")]
        [SerializeField]
        private Transform socket;

        [Tooltip("Sprite. Displayed on the player's interface.")]
        [SerializeField]
        private Sprite sprite;

        [Tooltip("Audio clip played when firing through this muzzle.")]
        [SerializeField]
        private AudioClip audioClipFire;

        [Header("Audio")]
        [Tooltip("AudioSource usado para reproduzir o som do tiro. Idealmente colocado no mesmo objecto do muzzle/socket.")]
        [SerializeField]
        private AudioSource audioSource;
        
        [Header("Particles")]
        
        [Tooltip("Firing Particles.")]
        [SerializeField]
        private GameObject prefabFlashParticles;

        [Tooltip("Number of particles to emit when firing.")]
        [SerializeField]
        private int flashParticlesCount = 5;

        [Header("Flash Light")]

        [Tooltip("Muzzle Flash Prefab. A small light we use when firing.")]
        [SerializeField]
        private GameObject prefabFlashLight;

        [Tooltip("Time that the light flashed stays active. After this time, it is disabled.")]
        [SerializeField]
        private float flashLightDuration;

        [Tooltip("Local offset applied to the light.")]
        [SerializeField]
        private Vector3 flashLightOffset;

        #endregion
        
        #region FIELDS

        /// <summary>
        /// Instantiated Particle System.
        /// </summary>
        private ParticleSystem particles;
        /// <summary>
        /// Instantiated light.
        /// </summary>
        private Light flashLight;

        #endregion

        #region UNITY FUNCTIONS

        /// <summary>
        /// Awake.
        /// </summary>
        private void Awake()
        {
            //Null Check.
            if(prefabFlashParticles != null)
            {
                //Instantiate Particles.
                GameObject spawnedParticlesPrefab = Instantiate(prefabFlashParticles, socket);
                //Reset the position.
                spawnedParticlesPrefab.transform.localPosition = default;
                //Reset the rotation.
                spawnedParticlesPrefab.transform.localEulerAngles = default;
                
                //Get Reference.
                particles = spawnedParticlesPrefab.GetComponent<ParticleSystem>();
            }

            //Null Check.
            if (prefabFlashLight)
            {
                //Instantiate.
                GameObject spawnedFlashLightPrefab = Instantiate(prefabFlashLight, socket);
                //Reset the position.
                spawnedFlashLightPrefab.transform.localPosition = flashLightOffset;
                //Reset the rotation.
                spawnedFlashLightPrefab.transform.localEulerAngles = default;
                
                //Get reference.
                flashLight = spawnedFlashLightPrefab.GetComponent<Light>();
                //Disable.
                flashLight.enabled = false;
            }

            // Tentar encontrar automaticamente um AudioSource se não estiver ligado no Inspector.
            if (audioSource == null)
            {
                // Primeiro tenta no próprio objecto.
                audioSource = GetComponent<AudioSource>();

                // Se ainda for nulo, tenta no socket.
                if (audioSource == null && socket != null)
                    audioSource = socket.GetComponent<AudioSource>();
            }
        }

        #endregion

        #region GETTERS

        public override void Effect()
        {
            // Partículas.
            if(particles != null)
                particles.Emit(flashParticlesCount);

            // Flash de luz.
            if (flashLight != null)
            {
                flashLight.enabled = true;
                StartCoroutine(nameof(DisableLight));
            }

            // ÁUDIO DO TIRO.
            if (audioClipFire != null)
            {
                if (audioSource != null)
                {
                    // Som 3D no próprio muzzle.
                    audioSource.PlayOneShot(audioClipFire);
                }
                else
                {
                    // Fallback: cria um áudio temporário na posição do socket.
                    Vector3 pos = socket != null ? socket.position : transform.position;
                    AudioSource.PlayClipAtPoint(audioClipFire, pos);
                }
            }
        }

        public override Transform GetSocket() => socket;

        public override Sprite GetSprite() => sprite;
        public override AudioClip GetAudioClipFire() => audioClipFire;
        
        public override ParticleSystem GetParticlesFire() => particles;
        public override int GetParticlesFireCount() => flashParticlesCount;
        
        public override Light GetFlashLight() => flashLight;
        public override float GetFlashLightDuration() => flashLightDuration;

        #endregion

        #region METHODS

        private IEnumerator DisableLight()
        {
            //Wait.
            yield return new WaitForSeconds(flashLightDuration);
            //Disable.
            flashLight.enabled = false;
        }

        #endregion
    }
}
