using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraControl: MonoBehaviour
{
    [Header("=== Целевая точка (фокус камеры на земле) ===")]
    [Tooltip("Пустышка, за которой следит камера. Создаётся автоматически, если пусто.")]
    public Transform focusPoint;

    [Header("=== Движение (WASD) ===")]
    [Tooltip("Базовая скорость перемещения")]
    public float moveSpeed = 20f;
    [Tooltip("Множитель ускорения на Shift")]
    public float sprintMultiplier = 2.5f;
    [Tooltip("Плавность разгона/торможения (чем больше — тем резче)")]
    public float moveSmoothing = 8f;

    [Header("=== Вращение (Q/E или средняя кнопка мыши) ===")]
    [Tooltip("Скорость поворота клавишами Q/E")]
    public float rotateSpeed = 90f;
    [Tooltip("Скорость поворота мышью при зажатой средней кнопке")]
    public float mouseRotateSpeed = 4f;
    [Tooltip("Плавность вращения")]
    public float rotateSmoothing = 10f;

    [Header("=== Наклон камеры (R/F или колёсико + Ctrl) ===")]
    [Tooltip("Угол обзора: минимум (почти горизонтально)")]
    public float minPitch = 25f;
    [Tooltip("Угол обзора: максимум (почти сверху)")]
    public float maxPitch = 80f;
    [Tooltip("Начальный угол наклона")]
    public float startPitch = 50f;
    [Tooltip("Скорость изменения наклона")]
    public float pitchSpeed = 60f;

    [Header("=== Зум (колёсико мыши) ===")]
    [Tooltip("Дистанция камеры от фокуса: минимум")]
    public float minDistance = 8f;
    [Tooltip("Дистанция камеры от фокуса: максимум")]
    public float maxDistance = 45f;
    [Tooltip("Начальная дистанция")]
    public float startDistance = 20f;
    [Tooltip("Скорость зума")]
    public float zoomSpeed = 15f;
    [Tooltip("Плавность зума")]
    public float zoomSmoothing = 10f;

    [Header("=== Границы карты (опционально) ===")]
    public bool useBounds = false;
    public Vector2 xBounds = new Vector2(-50f, 50f);
    public Vector2 zBounds = new Vector2(-50f, 50f);

    [Header("=== Краевой скролл (опционально, как в HoMM) ===")]
    public bool edgeScrollEnabled = false;
    [Tooltip("Толщина зоны у края экрана в пикселях")]
    public float edgeScrollBorder = 15f;

    // Внутренние переменные
    private float currentDistance;
    private float targetDistance;
    private float currentYaw;          // поворот вокруг вертикальной оси
    private float targetYaw;
    private float currentPitch;        // наклон
    private float targetPitch;

    private Vector3 currentVelocity;   // для SmoothDamp

    void Start()
    {
        // Создаём точку фокуса, если не задана
        if (focusPoint == null)
        {
            GameObject go = new GameObject("CameraFocusPoint");
            focusPoint = go.transform;
            focusPoint.position = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        // Инициализация углов из текущего положения камеры
        Vector3 dir = transform.position - focusPoint.position;
        currentDistance = targetDistance = Mathf.Clamp(dir.magnitude, minDistance, maxDistance);

        currentYaw = targetYaw = transform.eulerAngles.y;
        currentPitch = targetPitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
    }

    void Update()
    {
        HandleMovementInput();
        HandleRotationInput();
        HandlePitchInput();
        HandleZoomInput();
        ApplyTransform();
    }

    // ─────────────────────────────────────────────
    // ДВИЖЕНИЕ
    // ─────────────────────────────────────────────
    void HandleMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Краевой скролл (если включён) — работает по краям экрана
        if (edgeScrollEnabled)
        {
            Vector3 mp = Input.mousePosition;
            if (mp.x <= edgeScrollBorder) h -= 1f;
            else if (mp.x >= Screen.width - edgeScrollBorder) h += 1f;

            if (mp.y <= edgeScrollBorder) v -= 1f;
            else if (mp.y >= Screen.height - edgeScrollBorder) v += 1f;
        }

        Vector3 input = new Vector3(h, 0f, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Движение относительно текущего поворота камеры (вперёд = куда смотрит камера по земле)
        Vector3 forward = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.right;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= sprintMultiplier;

        Vector3 desired = (forward * input.z + right * input.x) * speed;
        currentVelocity = Vector3.Lerp(currentVelocity, desired, Time.deltaTime * moveSmoothing);

        Vector3 newPos = focusPoint.position + currentVelocity * Time.deltaTime;

        if (useBounds)
        {
            newPos.x = Mathf.Clamp(newPos.x, xBounds.x, xBounds.y);
            newPos.z = Mathf.Clamp(newPos.z, zBounds.x, zBounds.y);
        }

        focusPoint.position = newPos;
    }

    // ─────────────────────────────────────────────
    // ВРАЩЕНИЕ (Q / E или средняя кнопка мыши)
    // ─────────────────────────────────────────────
    void HandleRotationInput()
    {
        if (Input.GetKey(KeyCode.Q)) targetYaw -= rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) targetYaw += rotateSpeed * Time.deltaTime;

        // Зажатая средняя кнопка мыши — вращение мышью
        if (Input.GetMouseButton(2))
        {
            targetYaw += Input.GetAxis("Mouse X") * mouseRotateSpeed;
            targetPitch -= Input.GetAxis("Mouse Y") * mouseRotateSpeed;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
        }

        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotateSmoothing);
    }

    // ─────────────────────────────────────────────
    // НАКЛОН (R / F)
    // ─────────────────────────────────────────────
    void HandlePitchInput()
    {
        if (Input.GetKey(KeyCode.R)) targetPitch -= pitchSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.F)) targetPitch += pitchSpeed * Time.deltaTime;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * rotateSmoothing);
    }

    // ─────────────────────────────────────────────
    // ЗУМ
    // ─────────────────────────────────────────────
    void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
        {
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothing);
    }

    // ─────────────────────────────────────────────
    // ПРИМЕНЕНИЕ ПОЗИЦИИ И ПОВОРОТА
    // ─────────────────────────────────────────────
    void ApplyTransform()
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        // Позиция камеры — отступ назад и вверх от точки фокуса
        Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);
        transform.position = focusPoint.position + offset;

        // Смотрим на фокусную точку
        transform.rotation = Quaternion.LookRotation(focusPoint.position - transform.position, Vector3.up);
    }
}