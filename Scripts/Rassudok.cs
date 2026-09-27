using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SanityManager : MonoBehaviour
{
    [Header("Настройки рассудка")]
    [SerializeField] private float startingSanity = 100f; // Начальный рассудок
    [SerializeField] private float sanityDrainPerSecond = 1f; // Потеря в секунду

    [Header("UI - Sanity")]
    [SerializeField] private Image sanityFillImage;          // Image с Image Type = Filled
    [SerializeField] private Text sanityText;                // Опционально: текст "75 / 100"
    [SerializeField] private CanvasGroup sanityCanvasGroup;  // Опционально: для плавного скрытия/показа

    [Header("UI Colors")]
    [SerializeField] private Color sanityFullColor = new Color(0.6f, 0.4f, 1f); // Фиолетовый
    [SerializeField] private Color sanityLowColor = Color.red;
    [Range(0f, 1f)]
    [SerializeField] private float lowSanityThreshold = 0.25f; // Ниже 25% — красный

    public float currentSanity;
    private bool isGameOver = false;
    private bool isInitialized = false;

    public static SanityManager Instance { get; private set; }

    void Awake()
    {
        // Singleton для сохранения между сценами
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        InitializeSanity();
    }

    void Update()
    {
        if (!isGameOver && isInitialized)
        {
            // Потеря рассудка со временем
            currentSanity -= sanityDrainPerSecond * Time.deltaTime;
            currentSanity = Mathf.Max(0, currentSanity); // Не ниже 0

            UpdateUI();

            // Проверка окончания игры
            if (currentSanity <= 0)
            {
                isGameOver = true;
                SceneManager.LoadScene(3);
            }
        }
    }

    /// <summary>
    /// Инициализация рассудка (вызывается при старте и загрузке сцены)
    /// </summary>
    public void InitializeSanity()
    {
        currentSanity = startingSanity;
        isGameOver = false;
        isInitialized = true;
        UpdateUI();
        Debug.Log($"Sanity initialized: {currentSanity}/{startingSanity}");
    }

    /// <summary>
    /// Сброс рассудка к максимуму (для новых уровней)
    /// </summary>
    public void ResetSanityToMax()
    {
        currentSanity = startingSanity;
        isGameOver = false;
        isInitialized = true;
        UpdateUI();
        Debug.Log($"Sanity reset to max: {currentSanity}");
    }

    /// <summary>
    /// Восстановить рассудок (лечение)
    /// </summary>
    public void RestoreSanity(float amount)
    {
        if (!isGameOver)
        {
            currentSanity = Mathf.Min(startingSanity, currentSanity + amount);
            UpdateUI();
        }
    }

    /// <summary>
    /// Нанести урон рассудку
    /// </summary>
    public void DamageSanity(float amount)
    {
        if (!isGameOver)
        {
            currentSanity = Mathf.Max(0, currentSanity - amount);
            UpdateUI();

            if (currentSanity <= 0 && !isGameOver)
            {
                isGameOver = true;
                SceneManager.LoadScene(3);
            }
        }
    }

    // --- Обновление UI рассудка через fillAmount ---
    private void UpdateUI()
    {
        if (sanityFillImage != null)
        {
            float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;
            sanityFillImage.fillAmount = normalized; // 0..1

            // Цвет заливки: от красного (низ) к фиолетовому (полная)
            float t = Mathf.InverseLerp(0f, lowSanityThreshold, normalized);
            sanityFillImage.color = Color.Lerp(sanityLowColor, sanityFullColor, t);
        }

        if (sanityText != null)
        {
            sanityText.text = $"{Mathf.RoundToInt(currentSanity)} / {Mathf.RoundToInt(startingSanity)}";
        }

        if (sanityCanvasGroup != null)
        {
            // Плавное появление при потере рассудка (опционально)
            float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;
            float targetAlpha = normalized >= 0.999f ? 0f : 1f;
            sanityCanvasGroup.alpha = Mathf.MoveTowards(
                sanityCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Сброс рассудка при загрузке игровых сцен (не меню)
        if (scene.name != "MainMenu" && scene.name != "Loading_screen")
        {
            ResetSanityToMax();

            // Поиск Image в новой сцене, если ссылка потерялась
            if (sanityFillImage == null)
            {
                sanityFillImage = FindObjectOfType<Image>();
                if (sanityFillImage != null)
                {
                    Debug.Log("Sanity fill image found in new scene");
                    UpdateUI();
                }
            }
        }
    }

    // Геттеры для других скриптов
    public float CurrentSanity => currentSanity;
    public float MaxSanity => startingSanity;
    public float SanityNormalized => startingSanity > 0f ? currentSanity / startingSanity : 0f;
    public bool IsGameOver => isGameOver;
}