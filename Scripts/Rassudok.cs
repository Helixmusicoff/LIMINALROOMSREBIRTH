using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

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

    [Header("Death Screen")]
    [SerializeField] private CanvasGroup deathScreenCanvasGroup; // Canvas Group экрана смерти
    [SerializeField] private float deathScreenFadeDuration = 1.5f; // Длительность появления
    [SerializeField] private bool freezeGameOnDeath = true;        // Останавливать игру
    [SerializeField] private float delayBeforeFadeIn = 0.5f;       // Пауза перед появлением экрана

    [Header("Audio")]
    [SerializeField] private AudioSource heartbeatSource;          // Источник сердцебиения (loop)
    [SerializeField] private AudioSource sfxSource;                // Источник одноразовых звуков
    [SerializeField] private AudioClip heartbeatClip;              // Звук сердцебиения
    [SerializeField] private AudioClip deathJumpscareClip;         // Резкий звук при смерти
    [SerializeField] private AudioClip lowSanityWhisperClip;       // Шёпот при низком рассудке (опционально)
    [Range(0f, 1f)]
    [SerializeField] private float lowSanitySoundThreshold = 0.3f; // При каком % включать тревожные звуки

    public float currentSanity;
    private bool isGameOver = false;
    private bool isInitialized = false;
    private bool isDying = false;

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

        // Прячем экран смерти на старте
        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.alpha = 0f;
            deathScreenCanvasGroup.interactable = false;
            deathScreenCanvasGroup.blocksRaycasts = false;
        }
    }

    void Update()
    {
        if (!isGameOver && isInitialized)
        {
            // Потеря рассудка со временем
            currentSanity -= sanityDrainPerSecond * Time.deltaTime;
            currentSanity = Mathf.Max(0, currentSanity); // Не ниже 0

            UpdateUI();
            UpdateSanityAudio();

            // Проверка окончания игры
            if (currentSanity <= 0)
            {
                TriggerDeath();
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
        isDying = false;
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
        isDying = false;
        isInitialized = true;
        UpdateUI();

        // Прячем экран смерти
        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.alpha = 0f;
            deathScreenCanvasGroup.interactable = false;
            deathScreenCanvasGroup.blocksRaycasts = false;
        }

        // Возобновляем время
        Time.timeScale = 1f;

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
                TriggerDeath();
            }
        }
    }

    // --- Запуск смерти ---
    private void TriggerDeath()
    {
        if (isGameOver) return;
        isGameOver = true;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        isDying = true;

        // 1. Останавливаем сердцебиение
        if (heartbeatSource != null && heartbeatSource.isPlaying)
            heartbeatSource.Stop();

        // 2. Резкий звук (джампскер)
        if (sfxSource != null && deathJumpscareClip != null)
            sfxSource.PlayOneShot(deathJumpscareClip);

        // 3. Небольшая пауза перед появлением экрана
        yield return new WaitForSecondsRealtime(delayBeforeFadeIn);

        // 4. Останавливаем игру (важно: используем unscaled time ниже)
        if (freezeGameOnDeath)
            Time.timeScale = 0f;

        // 5. Показываем экран смерти
        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.blocksRaycasts = true;
            deathScreenCanvasGroup.interactable = true;

            float t = 0f;
            while (t < deathScreenFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                deathScreenCanvasGroup.alpha = Mathf.Clamp01(t / deathScreenFadeDuration);
                yield return null;
            }
            deathScreenCanvasGroup.alpha = 1f;
        }

        // 6. Разблокируем курсор (чтобы можно было нажать кнопки)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
            float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;
            float targetAlpha = normalized >= 0.999f ? 0f : 1f;
            sanityCanvasGroup.alpha = Mathf.MoveTowards(
                sanityCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 3f);
        }
    }

    // --- Звуки, зависящие от уровня рассудка ---
    private void UpdateSanityAudio()
    {
        float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;

        if (heartbeatSource == null || heartbeatClip == null) return;

        if (normalized <= lowSanitySoundThreshold)
        {
            // Включаем сердцебиение, если ещё не играет
            if (!heartbeatSource.isPlaying)
            {
                heartbeatSource.clip = heartbeatClip;
                heartbeatSource.loop = true;
                heartbeatSource.Play();
            }

            // Чем ниже рассудок — тем громче и быстрее
            float intensity = Mathf.InverseLerp(0f, lowSanitySoundThreshold, normalized);
            heartbeatSource.volume = Mathf.Lerp(1f, 0.3f, intensity);
            heartbeatSource.pitch = Mathf.Lerp(1.4f, 0.9f, intensity);

            // Опционально: шёпот
            if (sfxSource != null && lowSanityWhisperClip != null &&
                !sfxSource.isPlaying && Random.value < 0.005f)
            {
                sfxSource.PlayOneShot(lowSanityWhisperClip, 0.5f);
            }
        }
        else
        {
            // Выключаем сердцебиение, если рассудок в норме
            if (heartbeatSource.isPlaying)
                heartbeatSource.Stop();
        }
    }

    /// <summary>
    /// Перезапустить уровень после смерти (для кнопки на экране смерти)
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        isGameOver = false;
        isDying = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Загрузить сцену главного меню (для кнопки на экране смерти)
    /// </summary>
    public void LoadMainMenu(int menuSceneIndex = 0)
    {
        Time.timeScale = 1f;
        isGameOver = false;
        isDying = false;

        SceneManager.LoadScene(menuSceneIndex);
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

            // Поиск UI в новой сцене, если ссылка потерялась
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
    public bool IsDying => isDying;
}