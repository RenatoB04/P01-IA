using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FP_Controller_IS : MonoBehaviour
{
    // NOVO: Referência pública estática para a câmara (Acesso estável)
    public static Transform PlayerCameraRoot { get; private set; }

    [Header("Refs")]
    [SerializeField] Transform cameraRoot;
    CharacterController cc;
    Animator animator;

    [Header("Input Actions")]
    [SerializeField] InputActionReference move;
    [SerializeField] InputActionReference look;
    [SerializeField] InputActionReference jump;
    [SerializeField] InputActionReference sprint;
    [SerializeField] InputActionReference crouch;

    [Header("Velocidades")]
    public float walkSpeed = 6.5f;
    public float sprintSpeed = 10f;
    public float crouchSpeed = 3f;

    [Header("Aceleração (suavidade)")]
    public float accelGround = 14f;
    public float accelAir = 6f;

    [Header("Salto / Gravidade")]
    public float gravity = -28f;
    public float jumpHeight = 1.8f;
    public float maxFallSpeed = -50f;

    [Header("Câmara")]
    public float sens = 0.2f;
    float xRot;

    // estado
    Vector3 velocity;
    bool canJump = true;
    bool groundedPrev = true;

    // crouch (TOGGLE)
    [Header("Crouch (Toggle)")]
    public float crouchHeight = 1.0f;
    public float crouchCamYOffset = -0.4f;
    public float crouchSmooth = 12f;
    float originalHeight;
    float cameraRootBaseY;
    float stepOffsetOriginal;
    bool isCrouching;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        // CRÍTICO: Define a referência estática para a Weapon
        if (cameraRoot != null)
        {
            PlayerCameraRoot = cameraRoot;
        }

        originalHeight = cc.height;
        stepOffsetOriginal = cc.stepOffset;

        if (cameraRoot) cameraRootBaseY = cameraRoot.localPosition.y;
        else Debug.LogWarning("FP_Controller_IS: CameraRoot no Inspector.");

        cc.minMoveDistance = 0f;
        cc.slopeLimit = Mathf.Max(cc.slopeLimit, 45f);
        cc.stepOffset = Mathf.Max(cc.stepOffset, 0.3f);
        
        isCrouching = false;
        cc.height = originalHeight;
        cc.center = new Vector3(0f, originalHeight * 0.5f, 0f);
        if (cameraRoot)
        {
            var p = cameraRoot.localPosition;
            p.y = cameraRootBaseY;
            cameraRoot.localPosition = p;
        }
    }

    void OnEnable()
    {
        move.action.Enable();
        look.action.Enable();
        jump.action.Enable();
        if (sprint) sprint.action.Enable();
        if (crouch) crouch.action.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        move.action.Disable();
        look.action.Disable();
        jump.action.Disable();
        if (sprint) sprint.action.Disable();
        if (crouch) crouch.action.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // -------- Olhar --------
        Vector2 lookDelta = look.action.ReadValue<Vector2>();
        xRot = Mathf.Clamp(xRot - lookDelta.y * sens, -85f, 85f);
        if (cameraRoot) cameraRoot.localRotation = Quaternion.Euler(xRot, 0f, 0f);
        transform.Rotate(Vector3.up * (lookDelta.x * sens));

        // -------- Crouch (TOGGLE) --------
        if (crouch && crouch.action.WasPressedThisFrame())
            isCrouching = !isCrouching;
        
        // if (!isCrouching && CabeçaBateNoTeto()) isCrouching = true;

        float targetHeight  = isCrouching ? crouchHeight : originalHeight;
        float targetCenterY = targetHeight * 0.5f;

        cc.height = Mathf.Lerp(cc.height, targetHeight, Time.deltaTime * crouchSmooth);
        cc.center = Vector3.Lerp(cc.center, new Vector3(0f, targetCenterY, 0f), Time.deltaTime * crouchSmooth);
        cc.stepOffset = isCrouching ? 0.1f : stepOffsetOriginal;

        if (cameraRoot)
        {
            float targetCamY = cameraRootBaseY + (isCrouching ? crouchCamYOffset : 0f);
            Vector3 camLocal = cameraRoot.localPosition;
            camLocal.y = Mathf.Lerp(camLocal.y, targetCamY, Time.deltaTime * crouchSmooth);
            cameraRoot.localPosition = camLocal;
        }

        // -------- Movimento horizontal --------
        Vector2 m = move.action.ReadValue<Vector2>();
        Vector3 inputDir = (transform.right * m.x + transform.forward * m.y);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        // sprint desativado quando crouch
        bool sprinting = (sprint && sprint.action.IsPressed()) && !isCrouching;

        float targetSpeed = isCrouching ? crouchSpeed :
                            (sprinting ? sprintSpeed : walkSpeed);

        Vector3 targetHorizVel = inputDir * targetSpeed;
        float accel = cc.isGrounded ? accelGround : accelAir;

        Vector3 horiz = new Vector3(velocity.x, 0f, velocity.z);
        horiz = Vector3.MoveTowards(horiz, targetHorizVel, accel * Time.deltaTime);
        velocity.x = horiz.x;
        velocity.z = horiz.z;

        // -------- Atualizar parâmetros do Animator --------
        // "Speed"
        float speedPercent = new Vector3(velocity.x, 0f, velocity.z).magnitude / sprintSpeed;
        if (speedPercent < 0.05f) speedPercent = 0f;
        speedPercent = Mathf.Clamp01(speedPercent);

        animator.SetFloat("Speed", speedPercent, 0.1f, Time.deltaTime);
        animator.SetBool("isCrouching", isCrouching);

        // -------- Salto --------
        if (canJump && jump.action.WasPressedThisFrame() && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            canJump = false;
        }

        // -------- Gravidade --------
        velocity.y += gravity * Time.deltaTime;
        if (velocity.y < maxFallSpeed) velocity.y = maxFallSpeed;

        Vector3 motion = velocity * Time.deltaTime;
        CollisionFlags flags = cc.Move(motion);
        bool groundedNow = (flags & CollisionFlags.Below) != 0;

        if (groundedNow)
        {
            if (velocity.y < 0f) velocity.y = -2f;
            if (!groundedPrev) canJump = true;
        }

        groundedPrev = groundedNow;
    }
    
    /*
    bool CabeçaBateNoTeto()
    {
        Vector3 worldCenter = transform.TransformPoint(cc.center);
        float radius = cc.radius * 0.95f;
        Vector3 bottom = worldCenter + Vector3.up * (-cc.height * 0.5f + radius);
        Vector3 topIfStand = worldCenter + Vector3.up * (originalHeight * 0.5f - radius);
        return Physics.CheckCapsule(bottom, topIfStand, radius, ~0, QueryTriggerInteraction.Ignore);
    }
    */
}