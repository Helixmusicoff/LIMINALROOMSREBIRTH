using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleKinematicController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 20f;      // Сколько тратится в секунду при беге
    public float staminaRegenRate = 15f;      // Сколько восстанавливается в секунду
    public float staminaRegenDelay = 1f;      // Задержка перед началом восстановления
    public float staminaMinToSprint = 10f;    // Сколько нужно, чтобы снова начать бежать после истощения
    [Range(0f, 1f)] public float sprintMinThreshold = 0.05f; // Не бежим, если стамины меньше 5%

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float groundCheckDistance = 0.2f;
    public float groundCheckRadiusScale = 0.8f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.15f;

    [Header("Crouch")]
    public float crouchHeightScale = 0.5f;
    public float crouchCameraOffset = 0.6f;
    public float crouchTransitionSpeed = 8f;

    [Header("Camera")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;
    public bool lockCursor = true;

    [Header("Auto-fit collider to model (in Start)")]
    public bool autoFitColliderToModel = true;
    public Transform modelRoot;

    // --- Публичные свойства для UI ---
    public float Stamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaNormalized => currentStamina / maxStamina;
    public bool IsSprinting { get; private set; }
    public bool IsExhausted { get; private set; } // Кончилась стамина

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 horizontalVelocity;

    private float cameraPitch = 0f;
    private float cameraBaseY;
    private float currentCameraOffset = 0f;

    private float standHeight;
    private float standRadius;
    private Vector3 standCenter;
    private float crouchHeight;

    private bool isGrounded;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;

    private bool isCrouching;

    private float currentStamina;
    private float lastSprintTime = -999f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            cameraBaseY = cameraTransform.localPosition.y;

        if (autoFitColliderToModel)
            FitColliderToModel();

        standHeight = controller.height;
        standRadius = controller.radius;
        standCenter = controller.center;
        crouchHeight = standHeight * crouchHeightScale;

        currentStamina = maxStamina;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void FitColliderToModel()
    {
        Transform root = modelRoot != null ? modelRoot : transform;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = transform.InverseTransformPoint(b.center);
        Vector3 localSize = transform.InverseTransformVector(b.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        float height = Mathf.Max(localSize.y, 0.2f);
        float radius = Mathf.Max(localSize.x, localSize.z) * 0.5f;
        height = Mathf.Max(height, radius * 2f + 0.01f);
        radius = Mathf.Max(radius, 0.05f);

        controller.height = height;
        controller.radius = radius;
        controller.center = localCenter;
        controller.skinWidth = 0.02f;
    }

    void Update()
    {
        HandleGroundCheck();
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();   // здесь решается, бежим ли мы
        HandleStamina();    // тратим/восстанавливаем стамину
        HandleJump();
        ApplyGravity();
        MoveController();
    }

    private void HandleGroundCheck()
    {
        float bottomY = controller.center.y - controller.height * 0.5f + controller.radius;
        Vector3 origin = transform.position + Vector3.up * bottomY;
        float checkRadius = controller.radius * groundCheckRadiusScale;

        isGrounded = Physics.SphereCast(
            origin, checkRadius, Vector3.down, out _,
            groundCheckDistance, ~0, QueryTriggerInteraction.Ignore
        );

        if (!isGrounded && controller.isGrounded)
            isGrounded = true;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            if (velocity.y < 0f) velocity.y = -2f;
        }
    }

    private void HandleMouseLook()
    {
        if (cameraTransform == null) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f, Space.Self);

        cameraPitch -= my;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);
        cameraTransform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
    }

    private void HandleCrouch()
    {
        bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (!wantsCrouch && isCrouching && !CanStandUp())
            wantsCrouch = true;

        float targetHeight = wantsCrouch ? crouchHeight : standHeight;
        float targetOffset = wantsCrouch ? crouchCameraOffset : 0f;

        controller.height = Mathf.MoveTowards(
            controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        float bottomOfCapsule = standCenter.y - standHeight * 0.5f;
        controller.center = new Vector3(
            standCenter.x,
            bottomOfCapsule + controller.height * 0.5f,
            standCenter.z
        );

        currentCameraOffset = Mathf.MoveTowards(
            currentCameraOffset, targetOffset, crouchTransitionSpeed * Time.deltaTime);

        if (cameraTransform != null)
        {
            Vector3 cp = cameraTransform.localPosition;
            cp.y = cameraBaseY - currentCameraOffset;
            cameraTransform.localPosition = cp;
        }

        isCrouching = controller.height < standHeight - 0.05f;
    }

    private bool CanStandUp()
    {
        float checkRadius = standRadius * 0.95f;
        float bottomY = standCenter.y - standHeight * 0.5f + checkRadius;
        float topY = standCenter.y + standHeight * 0.5f - checkRadius;

        Vector3 p1 = transform.position + Vector3.up * bottomY;
        Vector3 p2 = transform.position + Vector3.up * topY;

        Collider[] hits = Physics.OverlapCapsule(
            p1, p2, checkRadius, ~0, QueryTriggerInteraction.Ignore);

        foreach (var h in hits)
        {
            if (h == controller) continue;
            if (h.transform.IsChildOf(transform)) continue;
            if (transform.IsChildOf(h.transform)) continue;
            return false;
        }
        return true;
    }

    // --- Движение: определяем скорость, но бег ещё не подтверждён ---
    private void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 forward = cameraTransform != null
            ? Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized
            : transform.forward;
        Vector3 right = cameraTransform != null
            ? Vector3.Scale(cameraTransform.right, new Vector3(1, 0, 1)).normalized
            : transform.right;

        Vector3 wishDir = (forward * z + right * x);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        bool wantSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isMoving = wishDir.sqrMagnitude > 0.01f;

        // Можем ли бежать прямо сейчас?
        bool canSprint =
            wantSprint &&
            isMoving &&
            !isCrouching &&
            !IsExhausted &&
            currentStamina > 0.01f;

        IsSprinting = canSprint;

        float targetSpeed;
        if (isCrouching) targetSpeed = crouchSpeed;
        else if (IsSprinting) targetSpeed = sprintSpeed;
        else targetSpeed = walkSpeed;

        horizontalVelocity = wishDir * targetSpeed;
    }

    // --- Стамина: трата и восстановление ---
    private void HandleStamina()
    {
        if (IsSprinting)
        {
            // Тратим
            currentStamina -= staminaDrainRate * Time.deltaTime;
            lastSprintTime = Time.time;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                IsExhausted = true; // бежать больше нельзя
            }
        }
        else
        {
            // Восстановление с задержкой
            if (Time.time - lastSprintTime >= staminaRegenDelay)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina > maxStamina)
                    currentStamina = maxStamina;
            }
        }

        // Снимаем "истощён" только когда накопили достаточно для бега
        if (IsExhausted && currentStamina >= staminaMinToSprint)
            IsExhausted = false;

        // Защита от дребезга
        if (currentStamina < maxStamina * sprintMinThreshold)
        {
            // Даже если IsExhausted ещё не сработал, порог мал — не критично
        }
    }

    private void HandleJump()
    {
        bool jumpDown = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);

        if (jumpDown)
            lastJumpPressedTime = Time.time;

        bool recentlyGrounded = (Time.time - lastGroundedTime) <= coyoteTime;
        bool bufferedJump = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (recentlyGrounded && bufferedJump && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -50f);
    }

    private void MoveController()
    {
        Vector3 motion = (horizontalVelocity + Vector3.up * velocity.y) * Time.deltaTime;
        controller.Move(motion);
    }
}