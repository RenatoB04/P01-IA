// Copyright 2021, Infima Games. All Rights Reserved.

using System;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using Unity.Netcode; // ADIÇÃO CRUCIAL: Necessário para a funcionalidade de rede

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Main Character Component. This component handles the most important functions of the character, and interfaces
    /// with basically every part of the asset, it is the hub where it all converges.
    /// </summary>
    [RequireComponent(typeof(CharacterKinematics))]
    // ALTERAÇÃO CRUCIAL: Substituído CharacterBehaviour por NetworkBehaviour
    public sealed class Character : NetworkBehaviour 
    {
       #region FIELDS SERIALIZED

       [Header("Network (Ligar no Inspector)")] // ADIÇÃO
       [Tooltip("A câmara principal do jogador (para ativar/desativar).")]
       [SerializeField] private Camera playerCamera;
       
       [Tooltip("O AudioListener principal (para ativar/desativar).")]
       [SerializeField] private AudioListener audioListener;
       
       [Tooltip("O script Kinematics (para ativar/desativar).")]
       [SerializeField] private CharacterKinematics characterKinematicsScript;

       [Header("Inventory")]
       [Tooltip("Inventory.")]
       [SerializeField]
       private InventoryBehaviour inventory;

       [Header("Cameras")]
       [Tooltip("Normal Camera.")]
       [SerializeField]
       private Camera cameraWorld;

       [Header("Animation")]
       [Tooltip("Determines how smooth the locomotion blendspace é.")]
       [SerializeField]
       private float dampTimeLocomotion = 0.15f;

       [Tooltip("How smoothly we play aiming transitions. Beware that this affects lots of things!")]
       [SerializeField]
       private float dampTimeAiming = 0.3f;
       
       [Header("Animation Procedural")]
       [Tooltip("Character Animator.")]
       [SerializeField]
       private Animator characterAnimator;

       [Header("Audio")]
       [Tooltip("AudioSource para sons da arma (reload, etc).")]
       [SerializeField]
       private AudioSource weaponAudioSource;

       #endregion

       #region FIELDS

       /// <summary>
       /// True if the character is aiming.
       /// </summary>
       private bool aiming;
       /// <summary>
       /// True if the character is running.
       /// </summary>
       private bool running;
       /// <summary>
       /// True if the character has its weapon holstered.
       /// </summary>
       private bool holstered;
       
       /// <summary>
       /// Last Time.time at which we shot.
       /// </summary>
       private float lastShotTime;
       
       /// <summary>
       /// Overlay Layer Index. Useful for playing things like firing animations.
       /// </summary>
       private int layerOverlay;
       /// <summary>
       /// Holster Layer Index. Used to play holster animations.
       /// </summary>
       private int layerHolster;
       /// <summary>
       /// Actions Layer Index. Used to play actions like reloading.
       /// </summary>
       private int layerActions;

       /// <summary>
       /// Character Kinematics. Handles all the IK stuff.
       /// </summary>
       private CharacterKinematics characterKinematics;
       
       /// <summary>
       /// The currently equipped weapon.
       /// </summary>
       private WeaponBehaviour equippedWeapon;
       /// <summary>
       /// The equipped weapon's attachment manager.
       /// </summary>
       private WeaponAttachmentManagerBehaviour weaponAttachmentManager;
       
       /// <summary>
       /// The scope equipped on the character's weapon.
       /// </summary>
       private ScopeBehaviour equippedWeaponScope;
       /// <summary>
       /// The magazine equipped on the character's weapon.
       /// </summary>
       private MagazineBehaviour equippedWeaponMagazine;
       
       /// <summary>
       /// True if the character is reloading.
       /// </summary>
       private bool reloading;
       
       /// <summary>
       /// True if the character is inspecting its weapon.
       /// </summary>
       private bool inspecting;

       /// <summary>
       /// True if the character is in the middle of holstering a weapon.
       /// </summary>
       private bool holstering;

       /// <summary>
       /// Look Axis Values.
       /// </summary>
       private Vector2 axisLook;
       /// <summary>
       /// Look Axis Values.
       /// </summary>
       private Vector2 axisMovement;
       
       /// <summary>
       /// True if the player is holding the aiming button.
       /// </summary>
       private bool holdingButtonAim;
       /// <summary>
       /// True if the player is holding the running button.
       /// </summary>
       private bool holdingButtonRun;
       /// <summary>
       /// True if the player is holding the firing button.
       /// </summary>
       private bool holdingButtonFire;

       /// <summary>
       /// If true, the tutorial text should be visible on screen.
       /// </summary>
       private bool tutorialTextVisible;

       /// <summary>
       /// True if the game cursor is locked! Used when pressing "Escape" to allow developers to more easily access the editor.
       /// </summary>
       private bool cursorLocked;

       // NOVO: arrays com TODAS as câmaras e listeners encontradas no prefab.
       private Camera[] allCameras;
       private AudioListener[] allAudioListeners;
       private PlayerInput cachedPlayerInput;

       #endregion

       #region CONSTANTS

       /// <summary>
       /// Aiming Alpha Value.
       /// </summary>
       private static readonly int HashAimingAlpha = Animator.StringToHash("Aiming");

       /// <summary>
       /// Hashed "Movement".
       /// </summary>
       private static readonly int HashMovement = Animator.StringToHash("Movement");

       #endregion
       
       // #############################################################
       // ADIÇÃO: Lógica de SPAWN e GESTÃO DE AUTORIDADE UNETCODE
       // #############################################################
       public override void OnNetworkSpawn()
       {
           base.OnNetworkSpawn();
           
           // Tentar obter refs principais se não tiverem sido ligadas
           if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
           if (!audioListener) audioListener = GetComponentInChildren<AudioListener>(true);
           if (!characterKinematicsScript) characterKinematicsScript = GetComponent<CharacterKinematics>();
           if (!weaponAudioSource) weaponAudioSource = GetComponentInChildren<AudioSource>(true);

           // Preencher arrays com TODAS as câmaras/listeners do prefab
           allCameras = GetComponentsInChildren<Camera>(true);
           allAudioListeners = GetComponentsInChildren<AudioListener>(true);

           if (cachedPlayerInput == null)
               cachedPlayerInput = GetComponent<PlayerInput>();

           bool owner = IsOwner;
           
           // Desativar/ativar TODAS as câmaras deste jogador, consoante seja dono ou não
           if (allCameras != null)
           {
               foreach (var cam in allCameras)
               {
                   if (cam != null)
                       cam.enabled = owner;
               }
           }

           // Ainda assim, manter compatibilidade com os campos antigos.
           if (playerCamera != null)
           {
              playerCamera.enabled = owner;

              if (owner)
              {
                 // Ajustes básicos seguros de câmera do owner.
                 playerCamera.nearClipPlane = 0.03f;
                 playerCamera.farClipPlane = 2000f;
                 // Não tocamos no culling mask aqui para não estragar o setup visual.
              }
           }

           // Desativar/ativar TODOS os AudioListeners deste jogador
           if (allAudioListeners != null)
           {
               foreach (var al in allAudioListeners)
               {
                   if (al != null)
                       al.enabled = owner;
               }
           }

           // Compat: listener principal
           if (audioListener) audioListener.enabled = owner;

           // IK apenas no dono
           if (characterKinematicsScript) characterKinematicsScript.enabled = owner;
           
           // Desativa o PlayerInput se não for o dono
           if (cachedPlayerInput != null)
               cachedPlayerInput.enabled = owner;
           else if (GetComponent<PlayerInput>() is PlayerInput pi)
               pi.enabled = owner;

           // GESTÃO DO CURSOR + inventário + spawn apenas no owner
           if (owner)
           {
               cursorLocked = true;
               UpdateCursorState(); 
               
               // ACIONAR O SPAWN (Chamamos o script responsável pela posição)
               if (TryGetComponent<NetworkSpawnHandler>(out var spawnHandler))
               {
                   // Não é ideal chamar manualmente, mas mantemos porque já o tinhas assim.
                   spawnHandler.OnNetworkSpawn(); 
               }
               
               // Inicializa inventário APÓS o spawn da rede
               if(inventory != null) inventory.Init(); 
               if(inventory != null) RefreshWeaponSetup();
           }
           else
           {
               cursorLocked = false; 
               Cursor.lockState = CursorLockMode.None;
               Cursor.visible = true;
           }
       }
       
       public override void OnNetworkDespawn()
       {
           base.OnNetworkDespawn();
           if (IsOwner)
           {
               Cursor.lockState = CursorLockMode.None;
               Cursor.visible = true;
           }
       }
       // #############################################################

       #region UNITY

       // CORREÇÃO: Removido 'override'
       protected void Awake()
       {
          // Tenta obter as refs de rede que não foram ligadas
          if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
          if (!audioListener) audioListener = GetComponentInChildren<AudioListener>(true);
          if (!characterKinematicsScript) characterKinematicsScript = GetComponent<CharacterKinematics>();

          // Tenta obter automaticamente um AudioSource para sons da arma se não estiver ligado no Inspector
          if (weaponAudioSource == null)
              weaponAudioSource = GetComponentInChildren<AudioSource>(true);

          // Cache de todas as câmaras/listeners e input
          allCameras = GetComponentsInChildren<Camera>(true);
          allAudioListeners = GetComponentsInChildren<AudioListener>(true);
          cachedPlayerInput = GetComponent<PlayerInput>();

          #region Lock Cursor
          cursorLocked = true;
          #endregion

          //Cache the CharacterKinematics component.
          characterKinematics = GetComponent<CharacterKinematics>();

          // LINHAS CORRIGIDAS: Comentamos para evitar o NRE do Weapon.cs no Awake()
          // inventory.Init(); 
          // RefreshWeaponSetup();
       }
       
       // CORREÇÃO: Removido 'override'
       protected void Start()
       {
          //Cache a reference to the holster layer's index.
          layerHolster = characterAnimator.GetLayerIndex("Layer Holster");
          //Cache a reference to the action layer's index.
          layerActions = characterAnimator.GetLayerIndex("Layer Actions");
          //Cache a reference to the overlay layer's index.
          layerOverlay = characterAnimator.GetLayerIndex("Layer Overlay");

          // CORREÇÃO DA ARMA: Removida a inicialização de armas, que agora está no OnNetworkSpawn()
          // if(inventory != null) inventory.Init(); 
          // if(inventory != null) RefreshWeaponSetup();
       }

       // CORREÇÃO: Removido 'override'
       protected void Update()
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL: Bloqueia lógica de input para remotos

          // Fallback: se estamos em reload mas a animação já não é de reload, libertar o flag.
          if (reloading)
          {
              var stateInfo = characterAnimator.GetCurrentAnimatorStateInfo(layerActions);
              if (!stateInfo.IsName("Reload") && !stateInfo.IsName("Reload Empty"))
              {
                  reloading = false;
              }
          }

          // Fallback: se estamos a holster e a animação de holster já terminou, libertar o flag.
          if (holstering)
          {
              var holsterInfo = characterAnimator.GetCurrentAnimatorStateInfo(layerHolster);
              if (!holsterInfo.IsName("Holster"))
              {
                  holstering = false;
              }
          }
          
          //Match Aim.
          aiming = holdingButtonAim && CanAim();
          //Match Run.
          running = holdingButtonRun && CanRun();

          //Holding the firing button.
          if (holdingButtonFire)
          {
             // ADIÇÃO DE PROTEÇÃO: Se a arma for nula, sair imediatamente (evita NRE)
             if (equippedWeapon == null) return; 

             //Check.
             if (CanPlayAnimationFire() && equippedWeapon.HasAmmunition() && equippedWeapon.IsAutomatic())
             {
                //Has fire rate passed.
                if (Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
                   Fire();
             }  
          }

          //Update Animator.
          UpdateAnimator();
       }

       // CORREÇÃO: Removido 'override'
       protected void LateUpdate()
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL: Bloqueia lógica de IK/câmara para remotos
          
          //We need a weapon for this!
          // ADIÇÃO DE PROTEÇÃO: Se a arma for nula, sair imediatamente (evita NRE)
          if (equippedWeapon == null)
             return;

          //Weapons without a scope should not be a thing! Ironsights are a scope too!
          if (equippedWeaponScope == null)
             return;
          
          //Make sure that we have a kinematics component!
          if(characterKinematics != null)
          {
             //Compute.
             characterKinematics.Compute();
          }
       }
       
       #endregion

       #region GETTERS

       // CORREÇÃO: Removido 'override' de todos os getters
       public Camera GetCameraWorld() => cameraWorld;

       public InventoryBehaviour GetInventory() => inventory;
       
       public bool IsCrosshairVisible() => !aiming && !holstered;
       
       public bool IsRunning() => running;
       
       public bool IsAiming() => aiming;
       
       public bool IsCursorLocked() => cursorLocked;
       
       public bool IsTutorialTextVisible() => tutorialTextVisible;
       
       public Vector2 GetInputMovement() => axisMovement;
       
       public Vector2 GetInputLook() => axisLook;

       #endregion

       #region METHODS

       /// <summary>
       /// Updates all the animator properties for this frame.
       /// </summary>
       private void UpdateAnimator()
       {
          //Movement Value. This value affects absolute movement. Aiming movement uses this, as opposed to per-axis movement.
          characterAnimator.SetFloat(HashMovement, Mathf.Clamp01(Mathf.Abs(axisMovement.x) + Mathf.Abs(axisMovement.y)), dampTimeLocomotion, Time.deltaTime);
          
          //Update the aiming value, but use interpolation. This makes sure that things like firing can transition properly.
          characterAnimator.SetFloat(HashAimingAlpha, Convert.ToSingle(aiming), 0.25f / 1.0f * dampTimeAiming, Time.deltaTime);

          //Update Animator Aiming.
          const string boolNameAim = "Aim";
          characterAnimator.SetBool(boolNameAim, aiming);
          
          //Update Animator Running.
          const string boolNameRun = "Running";
          characterAnimator.SetBool(boolNameRun, running);
       }
       
       /// <summary>
       /// Plays the inspect animation.
       /// </summary>
       private void Inspect()
       {
          //State.
          inspecting = true;
          //Play.
          characterAnimator.CrossFade("Inspect", 0.0f, layerActions, 0);
       }
       
       /// <summary>
       /// Fires the character's weapon.
       /// </summary>
       private void Fire()
       {
          //Save the shot time, so we can calculate the fire rate correctly.
          lastShotTime = Time.time;
          //Fire the weapon! Make sure that we also pass the scope's spread multiplier if we're aiming.
          equippedWeapon.Fire();

          //Play firing animation.
          const string stateName = "Fire";
          characterAnimator.CrossFade(stateName, 0.05f, layerOverlay, 0);
       }

       private void PlayReloadAnimation()
       {
          if (equippedWeapon == null)
              return;

          #region Animation

          //Get the name of the animation state to play, which depends on weapon settings, and ammunition!
          string stateName = equippedWeapon.HasAmmunition() ? "Reload" : "Reload Empty";
          //Play the animation state!
          characterAnimator.Play(stateName, layerActions, 0.0f);

          //Set.
          reloading = true;

          #endregion

          // ÁUDIO de reload.
          AudioClip reloadClip = equippedWeapon.HasAmmunition()
              ? equippedWeapon.GetAudioClipReload()
              : equippedWeapon.GetAudioClipReloadEmpty();

          if (reloadClip != null && weaponAudioSource != null)
          {
              weaponAudioSource.PlayOneShot(reloadClip);
          }

          //Reload lógico na arma (animação própria + munições).
          equippedWeapon.Reload();
       }

       /// <summary>
       /// Equip Weapon Coroutine.
       /// </summary>
       private IEnumerator Equip(int index = 0)
       {
          //Only if we're not holstered, holster. If we are already, we don't need to wait.
          if(!holstered)
          {
             //Holster.
             SetHolstered(holstering = true);
             //Wait.
             yield return new WaitUntil(() => holstering == false);
          }
          //Unholster. We do this just in case we were holstered.
          SetHolstered(false);
          //Play Unholster Animation.
          characterAnimator.Play("Unholster", layerHolster, 0);
          
          //Equip The New Weapon.
          inventory.Equip(index);
          //Refresh.
          RefreshWeaponSetup();
       }

       /// <summary>
       /// Refresh all weapon things to make sure we're all set up!
       /// </summary>
       private void RefreshWeaponSetup()
       {
          //Make sure we have a weapon. We don't want errors!
          if ((equippedWeapon = inventory.GetEquipped()) == null)
             return;
          
          //Update Animator Controller. We do this to update all animations to a specific weapon's set.
          characterAnimator.runtimeAnimatorController = equippedWeapon.GetAnimatorController();

          //Get the attachment manager so we can use it to get all the attachments!
          weaponAttachmentManager = equippedWeapon.GetAttachmentManager();
          if (weaponAttachmentManager == null) 
             return;
          
          //Get equipped scope. We need this one for its settings!
          equippedWeaponScope = weaponAttachmentManager.GetEquippedScope();
          //Get equipped magazine. We need this one for its settings!
          equippedWeaponMagazine = weaponAttachmentManager.GetEquippedMagazine();
       }

       private void FireEmpty()
       {
          /*
           * Save Time. Even though we're not actually firing, we still need this for the fire rate between
           * empty shots.
           */
          lastShotTime = Time.time;
          //Play.
          characterAnimator.CrossFade("Fire Empty", 0.05f, layerOverlay, 0);
       }

       /// <summary>
       /// Updates the cursor state based on the value of the cursorLocked variable.
       /// </summary>
       private void UpdateCursorState()
       {
          //Update cursor visibility.
          Cursor.visible = !cursorLocked;
          //Update cursor lock state.
          Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
       }

       /// <summary>
       /// Updates the "Holstered" variable, along with the Character's Animator value.
       /// </summary>
       private void SetHolstered(bool value = true)
       {
          //Update value.
          holstered = value;
          
          //Update Animator.
          const string boolName = "Holstered";
          characterAnimator.SetBool(boolName, holstered);    
       }
       
       #region ACTION CHECKS

       /// <summary>
       /// Can Fire.
       /// </summary>
       private bool CanPlayAnimationFire()
       {
          //Block.
          if (holstered || holstering)
             return false;

          //Block.
          if (reloading)
             return false;

          //Block.
          if (inspecting)
             return false;

          //Return.
          return true;
       }

       /// <summary>
       /// Determines if we can play the reload animation.
       /// </summary>
       private bool CanPlayAnimationReload()
       {
          //No reloading!
          if (reloading)
             return false;

          //Block while inspecting.
          if (inspecting)
             return false;
          
          //Return.
          return true;
       }

       /// <summary>
       /// Returns true if the character is able to holster their weapon.
       /// </summary>
       /// <returns></returns>
       private bool CanPlayAnimationHolster()
       {
          //Block.
          if (reloading)
             return false;

          //Block.
          if (inspecting)
             return false;
          
          //Return.
          return true;
       }

       /// <summary>
       /// Returns true if the Character can change their Weapon.
       /// </summary>
       /// <returns></returns>
       private bool CanChangeWeapon()
       {
          //Block.
          if (holstering)
             return false;

          //Block.
          if (reloading)
             return false;

          //Block.
          if (inspecting)
             return false;
          
          //Return.
          return true;
       }

       /// <summary>
       /// Returns true if the Character can play the Inspect animation.
       /// </summary>
       private bool CanPlayAnimationInspect()
       {
          //Block.
          if (holstered || holstering)
             return false;

          //Block.
          if (reloading)
             return false;

          //Block.
          if (inspecting)
             return false;
          
          //Return.
          return true;
       }

       /// <summary>
       /// Returns true if the Character can Aim.
       /// </summary>
       /// <returns></returns>
       private bool CanAim()
       {
          //Block.
          if (holstered || inspecting)
             return false;

          //Block.
          if (reloading || holstering)
             return false;
          
          //Return.
          return true;
       }
       
       /// <summary>
       /// Returns true if the character can run.
       /// </summary>
       /// <returns></returns>
       private bool CanRun()
       {
          //Block.
          if (inspecting)
             return false;

          //Block.
          if (reloading || aiming)
             return false;

          //While trying to fire, we don't want to run. We do this just in case we do fire.
          if (holdingButtonFire && equippedWeapon != null && equippedWeapon.HasAmmunition())
             return false;

          //This blocks running backwards, or while fully moving sideways.
          if (axisMovement.y <= 0 || Math.Abs(Mathf.Abs(axisMovement.x) - 1) < 0.01f)
             return false;
          
          //Return.
          return true;
       }

       #endregion

       #region INPUT

       /// <summary>
       /// Fire.
       /// </summary>
       public void OnTryFire(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          // ADIÇÃO DE PROTEÇÃO: Se a arma for nula, sair imediatamente (evita NRE)
          if (equippedWeapon == null) return; 

          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;

          //Switch.
          switch (context)
          {
             //Started.
             case {phase: InputActionPhase.Started}:
                //Hold.
                holdingButtonFire = true;
                break;
             //Performed.
             case {phase: InputActionPhase.Performed}:
                //Ignore if we're not allowed to actually fire.
                if (!CanPlayAnimationFire())
                   break;
                
                //Check.
                if (equippedWeapon.HasAmmunition())
                {
                   //Check.
                   if (equippedWeapon.IsAutomatic())
                      break;
                      
                   //Has fire rate passed.
                   if (Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
                      Fire();
                }
                //Fire Empty.
                else
                   FireEmpty();
                break;
             //Canceled.
             case {phase: InputActionPhase.Canceled}:
                //Stop Hold.
                holdingButtonFire = false;
                break;
          }
       }
       /// <summary>
       /// Reload.
       /// </summary>
       public void OnTryPlayReload(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;
          
          //Block.
          if (!CanPlayAnimationReload())
             return;
          
          //Switch.
          switch (context)
          {
             //Performed.
             case {phase: InputActionPhase.Performed}:
                //Play Animation.
                PlayReloadAnimation();
                break;
          }
       }
       
       /// <summary>
       /// Inspect.
       /// </summary>
       public void OnTryInspect(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;
          
          //Block.
          if (!CanPlayAnimationInspect())
             return;
          
          //Switch.
          switch (context)
          {
             //Performed.
             case {phase: InputActionPhase.Performed}:
                //Play Animation.
                Inspect();
                break;
          }
       }
       /// <summary>
       /// Aiming.
       /// </summary>
       public void OnTryAiming(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;

          //Switch.
          switch (context.phase)
          {
             case InputActionPhase.Started:
                //Started.
                holdingButtonAim = true;
                break;
             case InputActionPhase.Canceled:
                //Canceled.
                holdingButtonAim = false;
                break;
          }
       }

       /// <summary>
       /// Holster.
       /// </summary>
       public void OnTryHolster(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;
          
          //Switch.
          switch (context.phase)
          {
             //Performed.
             case InputActionPhase.Performed:
                //Check.
                if (CanPlayAnimationHolster())
                {
                   //Set.
                   SetHolstered(!holstered);
                   //Holstering.
                   holstering = true;
                }
                break;
          }
       }
       /// <summary>
       /// Run. 
       /// </summary>
       public void OnTryRun(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;
          
          //Switch.
          switch (context.phase)
          {
             //Started.
             case InputActionPhase.Started:
                //Start.
                holdingButtonRun = true;
                break;
             //Canceled.
             case InputActionPhase.Canceled:
                //Stop.
                holdingButtonRun = false;
                break;
          }
       }
       /// <summary>
       /// Next Inventory Weapon.
       /// </summary>
       public void OnTryInventoryNext(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Block while the cursor is unlocked.
          if (!cursorLocked)
             return;
          
          //Null Check.
          if (inventory == null)
             return;
          
          //Switch.
          switch (context)
          {
             //Performed.
             case {phase: InputActionPhase.Performed}:
                //Get the index increment direction for our inventory using the scroll wheel direction. If we're not
                //actually using one, then just increment by one.
                float scrollValue = context.valueType.IsEquivalentTo(typeof(Vector2)) ? Mathf.Sign(context.ReadValue<Vector2>().y) : 1.0f;
                
                //Get the next index to switch to.
                int indexNext = scrollValue > 0 ? inventory.GetNextIndex() : inventory.GetLastIndex();
                //Get the current weapon's index.
                int indexCurrent = inventory.GetEquippedIndex();
                
                //Make sure we're allowed to change, and also that we're not using the same index, otherwise weird things happen!
                if (CanChangeWeapon() && (indexCurrent != indexNext))
                   StartCoroutine(nameof(Equip), indexNext);
                break;
          }
       }
       
       public void OnLockCursor(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Switch.
          switch (context)
          {
             //Performed.
             case {phase: InputActionPhase.Performed}:
                //Toggle the cursor locked value.
                cursorLocked = !cursorLocked;
                //Update the cursor's state.
                UpdateCursorState();
                break;
          }
       }
       
       /// <summary>
       /// Movement.
       /// </summary>
       public void OnMove(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Read.
          axisMovement = cursorLocked ? context.ReadValue<Vector2>() : default;
       }
       /// <summary>
       /// Look.
       /// </summary>
       public void OnLook(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Read.
          axisLook = cursorLocked ? context.ReadValue<Vector2>() : default;
       }

       /// <summary>
       /// Called in order to update the tutorial text value.
       /// </summary>
       public void OnUpdateTutorial(InputAction.CallbackContext context)
       {
          if (!IsOwner) return; // ADIÇÃO CRUCIAL
          
          //Switch.
          tutorialTextVisible = context switch
          {
             //Started. Show the tutorial.
             {phase: InputActionPhase.Started} => true,
             //Canceled. Hide the tutorial.
             {phase: InputActionPhase.Canceled} => false,
             //Default.
             _ => tutorialTextVisible
          };
       }

       #endregion

       #region ANIMATION EVENTS

       // CORREÇÃO: Removido 'override' de todos os métodos de evento de animação
       public void EjectCasing()
       {
          //Notify the weapon.
          if(equippedWeapon != null)
             equippedWeapon.EjectCasing();
       }
       public void FillAmmunition(int amount)
       {
          //Notify the weapon to fill the ammunition by the amount.
          if(equippedWeapon != null)
             equippedWeapon.FillAmmunition(amount);
       }
       
       public void SetActiveMagazine(int active)
       {
          //Set magazine gameObject active.
          if(equippedWeaponMagazine != null) equippedWeaponMagazine.gameObject.SetActive(active != 0);
       }
       
       public void AnimationEndedReload()
       {
          //Stop reloading!
          reloading = false;
       }

       public void AnimationEndedInspect()
       {
          //Stop Inspecting.
          inspecting = false;
       }
       public void AnimationEndedHolster()
       {
          //Stop Holstering!
          holstering = false;
       }

       #endregion
       // (fim da classe)
    }
    #endregion
}