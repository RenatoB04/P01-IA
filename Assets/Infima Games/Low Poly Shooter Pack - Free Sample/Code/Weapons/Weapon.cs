// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using Unity.Netcode; // Necessário para integração com o PlayerWeaponController

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Weapon. Gere a lógica de disparo, animações e VFX/SFX da arma.
    /// </summary>
    public class Weapon : WeaponBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Network (Integração RPC)")]
        [Tooltip("Referência ao controlador de rede (PlayerWeaponController) no root do jogador.")]
        [SerializeField]
        private PlayerWeaponController networkWeaponProxy;

        [Header("Firing")]
        [Tooltip("Define se a arma é automática (mantendo o botão de disparo pressionado).")]
        [SerializeField] 
        private bool automatic;

        [Tooltip("Velocidade inicial do projétil.")]
        [SerializeField]
        private float projectileImpulse = 400.0f;

        [Tooltip("Taxa de disparo (tiros por minuto).")]
        [SerializeField] 
        private int roundsPerMinutes = 200;

        [Tooltip("Máscara de colisão usada no raycast.")]
        [SerializeField]
        private LayerMask mask;

        [Tooltip("Distância máxima de disparo precisa.")]
        [SerializeField]
        private float maximumDistance = 500.0f;

        [Header("Animation")]
        [Tooltip("Ponto de ejeção das cápsulas.")]
        [SerializeField]
        private Transform socketEjection;

        [Header("Resources")]
        [Tooltip("Prefab das cápsulas.")]
        [SerializeField]
        private GameObject prefabCasing;

        [Tooltip("Prefab do projétil.")]
        [SerializeField]
        private GameObject prefabProjectile;

        [Tooltip("Animator Controller necessário para esta arma.")]
        [SerializeField] 
        public RuntimeAnimatorController controller;

        [Tooltip("Sprite do corpo da arma.")]
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

        private MagazineBehaviour magazineBehaviour;
        private MuzzleBehaviour muzzleBehaviour;

        private Character characterBehaviour;
        private Transform playerCamera;

        #endregion

        #region UNITY

        protected void Awake()
        {
            // Obter referências básicas.
            animator = GetComponent<Animator>();
            attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();
            characterBehaviour = GetComponentInParent<Character>();
            networkWeaponProxy = GetComponentInParent<PlayerWeaponController>();

            // Câmara do jogador.
            if (characterBehaviour != null)
                playerCamera = characterBehaviour.GetCameraWorld()?.transform;
        }

        protected void Start()
        {
            // Cache de attachments.
            magazineBehaviour = attachmentManager.GetEquippedMagazine();
            muzzleBehaviour = attachmentManager.GetEquippedMuzzle();

            // Preencher munição.
            if (magazineBehaviour != null)
                ammunitionCurrent = magazineBehaviour.GetAmmunitionTotal();
            else
                ammunitionCurrent = 0;
        }

        #endregion

        #region GETTERS

        public override Animator GetAnimator() => animator;
        public override Sprite GetSpriteBody() => spriteBody;
        public override AudioClip GetAudioClipHolster() => audioClipHolster;
        public override AudioClip GetAudioClipUnholster() => audioClipUnholster;
        public override AudioClip GetAudioClipReload() => audioClipReload;
        public override AudioClip GetAudioClipReloadEmpty() => audioClipReloadEmpty;
        public override AudioClip GetAudioClipFireEmpty() => audioClipFireEmpty;
        public override AudioClip GetAudioClipFire() => muzzleBehaviour != null ? muzzleBehaviour.GetAudioClipFire() : null;
        public override int GetAmmunitionCurrent() => ammunitionCurrent;
        public override int GetAmmunitionTotal() => magazineBehaviour.GetAmmunitionTotal();
        public override bool IsAutomatic() => automatic;
        public override float GetRateOfFire() => roundsPerMinutes;
        public override bool IsFull() => ammunitionCurrent == magazineBehaviour.GetAmmunitionTotal();
        public override bool HasAmmunition() => ammunitionCurrent > 0;
        public override RuntimeAnimatorController GetAnimatorController() => controller;
        public override WeaponAttachmentManagerBehaviour GetAttachmentManager() => attachmentManager;

        #endregion

        #region METHODS

        public override void Reload()
        {
            animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f);
        }

        /// <summary>
        /// Método chamado ao disparar.
        /// Gere o som local (muzzle), animações e envia o RPC de rede.
        /// </summary>
        public override void Fire(float spreadMultiplier = 1.0f)
        {
            if (muzzleBehaviour == null || playerCamera == null)
                return;

            if (networkWeaponProxy == null)
            {
                Debug.LogError("PlayerWeaponController nulo! Não é possível disparar em rede.");
                return;
            }

            Transform muzzleSocket = muzzleBehaviour.GetSocket();

            // Direção e origem do disparo.
            Quaternion rotation = Quaternion.LookRotation(playerCamera.forward * 1000.0f - muzzleSocket.position);
            Vector3 fireDirection = playerCamera.forward;
            Vector3 fireOrigin = muzzleSocket.position;

            // Ajustar direção caso o raycast atinja algo.
            if (Physics.Raycast(new Ray(playerCamera.position, playerCamera.forward), out RaycastHit hit, maximumDistance, mask))
            {
                rotation = Quaternion.LookRotation(hit.point - muzzleSocket.position);
                fireDirection = (hit.point - fireOrigin).normalized;
            }

            // Animação e redução de munição.
            animator.Play("Fire", 0, 0.0f);
            ammunitionCurrent = Mathf.Clamp(ammunitionCurrent - 1, 0, magazineBehaviour.GetAmmunitionTotal());

            // Efeito local do muzzle (som + flash).
            muzzleBehaviour.Effect();

            // Chamar RPC de rede para spawnar a bala e replicar efeitos.
            networkWeaponProxy.FireExternally(fireDirection, fireOrigin, projectileImpulse);
        }

        /// <summary>
        /// Permite que outros clientes (via RPC) reproduzam o som e efeito do muzzle.
        /// </summary>
        public void PlayMuzzleEffect()
        {
            if (muzzleBehaviour != null)
                muzzleBehaviour.Effect();
        }

        public override void FillAmmunition(int amount)
        {
            ammunitionCurrent = amount != 0
                ? Mathf.Clamp(ammunitionCurrent + amount, 0, GetAmmunitionTotal())
                : magazineBehaviour.GetAmmunitionTotal();
        }

        public override void EjectCasing()
        {
            if (prefabCasing != null && socketEjection != null)
                Instantiate(prefabCasing, socketEjection.position, socketEjection.rotation);
        }

        #endregion
    }
}
