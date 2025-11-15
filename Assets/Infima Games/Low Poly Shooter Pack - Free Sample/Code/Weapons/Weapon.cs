// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace InfimaGames.LowPolyShooterPack
{
    [RequireComponent(typeof(WeaponConfig))]
    public class Weapon : WeaponBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Network (Integração RPC)")]
        [SerializeField]
        private PlayerWeaponController networkWeaponProxy;

        [Header("Firing")]
        [SerializeField]
        private bool automatic;
        [SerializeField]
        private float projectileImpulse = 400.0f;
        [SerializeField]
        private int roundsPerMinutes = 200;
        [SerializeField]
        private LayerMask mask;
        [SerializeField]
        private float maximumDistance = 500.0f;

        [Header("Animation")]
        [SerializeField]
        private Transform socketEjection;

        [Header("Resources")]
        [SerializeField]
        private GameObject prefabCasing;
        [SerializeField]
        private GameObject prefabProjectile;
        [SerializeField]
        public RuntimeAnimatorController controller;
        [SerializeField]
        private Sprite spriteBody;

        [Header("Audio Clips Holster")]
        [SerializeField] private AudioClip audioClipHolster;
        [SerializeField] private AudioClip audioClipUnholster;

        [Header("Audio Clips Reloads")]
        [SerializeField] private AudioClip audioClipReload;
        [SerializeField] private AudioClip audioClipReloadEmpty;

        [Header("Audio Clips Other")]
        [SerializeField] private AudioClip audioClipFireEmpty;

        #endregion

        #region FIELDS

        private Animator animator;
        private WeaponAttachmentManagerBehaviour attachmentManager;

        private int ammunitionCurrent;
        private int ammunitionReserve;

        private MagazineBehaviour magazineBehaviour;
        private MuzzleBehaviour muzzleBehaviour;

        private Character characterBehaviour;
        private Transform playerCamera;

        private WeaponConfig config;
        private bool isReloading;

        #endregion

        #region UNITY

        protected void Awake()
        {
            animator = GetComponent<Animator>();
            attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();
            characterBehaviour = GetComponentInParent<Character>();
            networkWeaponProxy = GetComponentInParent<PlayerWeaponController>();

            config = GetComponent<WeaponConfig>();
            if (config == null)
                Debug.LogError("Weapon.cs não encontrou o WeaponConfig.cs!");

            if (characterBehaviour != null)
                playerCamera = characterBehaviour.GetCameraWorld()?.transform;
        }

        protected void Start()
        {
            StartCoroutine(SetupWeaponUI());
        }

        private IEnumerator SetupWeaponUI()
        {
            magazineBehaviour = attachmentManager.GetEquippedMagazine();
            muzzleBehaviour = attachmentManager.GetEquippedMuzzle();

            ammunitionCurrent = config.magSize;
            ammunitionReserve = config.startingReserve;

            isReloading = false;

            while (AmmoUI.Instance == null)
            {
                yield return null;
            }

            UpdateAmmoUI();
        }

        #endregion

        #region GETTERS

        public override Animator GetAnimator() => animator;
        public override Sprite GetSpriteBody() => spriteBody;
        public override AudioClip GetAudioClipHolster() => audioClipHolster;
        public override AudioClip GetAudioClipUnholster() => audioClipUnholster;

        public override AudioClip GetAudioClipReload() => config.reloadSfx != null ? config.reloadSfx : audioClipReload;
        public override AudioClip GetAudioClipReloadEmpty() => config.reloadSfx != null ? config.reloadSfx : audioClipReloadEmpty;
        public override AudioClip GetAudioClipFireEmpty() => config.emptyClickSfx != null ? config.emptyClickSfx : audioClipFireEmpty;
        public override AudioClip GetAudioClipFire()
        {
            if (muzzleBehaviour != null && muzzleBehaviour.GetAudioClipFire() != null)
                return muzzleBehaviour.GetAudioClipFire();
            return config.fireSfx != null ? config.fireSfx : null;
        }

        public override int GetAmmunitionCurrent() => ammunitionCurrent;
        public override int GetAmmunitionTotal() => config.magSize;

        public override bool IsAutomatic() => automatic;

        public override float GetRateOfFire()
        {
            return config.fireRate > 0.01f ? (60.0f / config.fireRate) : roundsPerMinutes;
        }

        public override bool IsFull() => ammunitionCurrent == config.magSize;
        public override bool HasAmmunition() => ammunitionCurrent > 0;
        public override RuntimeAnimatorController GetAnimatorController() => controller;
        public override WeaponAttachmentManagerBehaviour GetAttachmentManager() => attachmentManager;

        #endregion

        #region METHODS

        public override void Reload()
        {
            if (isReloading || ammunitionReserve <= 0 || IsFull())
                return;

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f);

            yield return new WaitForSeconds(config.reloadTime);

            int bulletsNeeded = config.magSize - ammunitionCurrent;
            int bulletsToTake = Mathf.Min(ammunitionReserve, bulletsNeeded);

            if (bulletsToTake > 0)
            {
                ammunitionCurrent += bulletsToTake;
                ammunitionReserve -= bulletsToTake;
            }

            UpdateAmmoUI();
            isReloading = false;
        }


        public override void Fire(float spreadMultiplier = 1.0f)
        {
            if (isReloading) return;

            if (!HasAmmunition())
            {
                if ((config.emptyClickSfx != null || audioClipFireEmpty != null) && animator != null)
                    animator.Play("Fire Empty", 0, 0.0f);
                return;
            }

            if (muzzleBehaviour == null || playerCamera == null) return;
            if (networkWeaponProxy == null)
            {
                Debug.LogError("PlayerWeaponController nulo!");
                return;
            }

            Transform muzzleSocket = muzzleBehaviour.GetSocket();
            Vector3 fireDirection = playerCamera.forward;
            Vector3 fireOrigin = muzzleSocket.position;

            float dist = config.maxAimDistance > 0 ? config.maxAimDistance : maximumDistance;

            if (Physics.Raycast(new Ray(playerCamera.position, playerCamera.forward), out RaycastHit hit, dist, mask))
            {
                fireDirection = (hit.point - fireOrigin).normalized;
            }

            animator.Play("Fire", 0, 0.0f);
            ammunitionCurrent = Mathf.Clamp(ammunitionCurrent - 1, 0, config.magSize);

            UpdateAmmoUI();

            muzzleBehaviour.Effect();

            networkWeaponProxy.FireExternally(fireDirection, fireOrigin, projectileImpulse);

            if (!HasAmmunition() && ammunitionReserve > 0)
            {
                Reload();
            }
        }

        // --- CORREÇÃO DO ERRO CS0115 ---
        // Removido 'override'
        public void PlayMuzzleEffect()
        {
            if (muzzleBehaviour != null)
                muzzleBehaviour.Effect();
        }
        // --- FIM DA CORREÇÃO ---

        public override void FillAmmunition(int amount)
        {
            // Ignorado. O 'ReloadCoroutine' trata de tudo.
        }

        public override void EjectCasing()
        {
            if (prefabCasing != null && socketEjection != null)
                Instantiate(prefabCasing, socketEjection.position, socketEjection.rotation);
        }

        private void UpdateAmmoUI()
        {
            if (networkWeaponProxy != null && !networkWeaponProxy.IsOwner)
                return;
            if (AmmoUI.Instance == null)
                return;

            AmmoUI.Instance.Set(ammunitionCurrent, ammunitionReserve);
        }

        #endregion
    }
}