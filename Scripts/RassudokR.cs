using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SanityManager : MonoBehaviour
{
    [Header("Настройки рассудка")]
    [SerializeField] private float startingSanity = 100f;
    [SerializeField] private float sanityDrainPerSecond = 1f;

    [Header("UI - Sanity")]
    [SerializeField] private Image sanityFillImage;
    [SerializeField] private Text sanityText;
    [SerializeField] private CanvasGroup sanityCanvasGroup;

    [Header("UI Colors")]
    [SerializeField] private Color sanityFullColor = new Color(0.6f, 0.4f, 1f);
    [SerializeField] private Color sanityLowColor = Color.red;
    [Range(0f, 1f)]
    [SerializeField] private float lowSanityThreshold = 0.25f;

    [Header("Audio - Heartbeat")]
    [SerializeField] private AudioSource heartbeatSource;
    [SerializeField] private AudioClip heartbeatClip;
    [Range(0f, 1f)]
    [SerializeField] private float lowSanitySoundThreshold = 0.3f;

    [Header("Audio - Extra (шёпот, дрон, гул)")]
    [SerializeField] private AudioSource whisperSource;      // для случайных шёпотов
    [SerializeField] private AudioSource droneSource;        // для постоянного гула/дрона на низком рассудке
    [SerializeField] private AudioClip[] whisperClips;       // массив разных шёпотов
    [SerializeField] private AudioClip lowSanityDroneClip;   // гул/звон в ушах
    [SerializeField] private float whisperIntervalMin = 4f;
    [SerializeField] private float whisperIntervalMax = 10f;

    [Header("Death Screen")]
    [SerializeField] private GameObject deathScreen;         // панель/канвас смерти
    [SerializeField] private CanvasGroup deathScreenCanvasGroup; // опционально, для плавного появления
    [SerializeField] private float deathFadeDuration = 1.5f;
    [SerializeField] private bool freezeTimeOnDeath = true;
    [SerializeField] private string restartSceneName = "";   // если пусто — перезагрузит текущую сцену

    public float currentSanity;
    private bool isInitialized = false;
    private bool isDead = false;

    private float whisperTimer = 0f;
    private float nextWhisperTime = 0f;

    public static SanityManager Instance { get; private set; }

    // События для других систем
    public event System.Action OnSanityDepleted;
    public event System.Action<float> OnSanityChanged;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (heartbeatSource != null && heartbeatSource.isPlaying)
            heartbeatSource.Stop();

        if (droneSource != null && droneSource.isPlaying)
            droneSource.Stop();

        if (deathScreen != null)
            deathScreen.SetActive(false);

        if (deathScreenCanvasGroup != null)
            deathScreenCanvasGroup.alpha = 0f;

        InitializeSanity();
    }

    void Update()
    {
        if (!isInitialized || isDead) return;

        currentSanity -= sanityDrainPerSecond * Time.deltaTime;
        currentSanity = Mathf.Max(0, currentSanity);

        OnSanityChanged?.Invoke(currentSanity);

        UpdateUI();
        UpdateSanityAudio();
        UpdateWhispers();

        if (currentSanity <= 0f && !isDead)
        {
            TriggerDeath();
        }
    }

    public void InitializeSanity()
    {
        currentSanity = startingSanity;
        isInitialized = true;
        isDead = false;
        UpdateUI();
        Debug.Log($"[Sanity] Initialized: {currentSanity}/{startingSanity}");
    }

    public void ResetSanityToMax()
    {
        currentSanity = startingSanity;
        isInitialized = true;
        isDead = false;

        if (heartbeatSource != null && heartbeatSource.isPlaying)
            heartbeatSource.Stop();

        if (droneSource != null && droneSource.isPlaying)
            droneSource.Stop();

        UpdateUI();
        Debug.Log($"[Sanity] Reset to max: {currentSanity}");
    }

    public void RestoreSanity(float amount)
    {
        currentSanity = Mathf.Min(startingSanity, currentSanity + amount);
        UpdateUI();
    }

    public void DamageSanity(float amount)
    {
        currentSanity = Mathf.Max(0, currentSanity - amount);
        UpdateUI();

        if (currentSanity <= 0f && !isDead)
            TriggerDeath();
    }

    // ---------- UI ----------
    private void UpdateUI()
    {
        float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;

        if (sanityFillImage != null)
        {
            sanityFillImage.fillAmount = normalized;

            float t = Mathf.InverseLerp(0f, lowSanityThreshold, normalized);
            sanityFillImage.color = Color.Lerp(sanityLowColor, sanityFullColor, t);
        }

        if (sanityText != null)
        {
            sanityText.text = $"{Mathf.RoundToInt(currentSanity)} / {Mathf.RoundToInt(startingSanity)}";
        }

        if (sanityCanvasGroup != null)
        {
            float targetAlpha = normalized >= 0.999f ? 0f : 1f;
            sanityCanvasGroup.alpha = Mathf.MoveTowards(
                sanityCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 3f);
        }
    }

    // ---------- Audio: сердцебиение + дрон ----------
    private void UpdateSanityAudio()
    {
        float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;

        // ---- Heartbeat ----
        if (heartbeatSource != null && heartbeatClip != null)
        {
            if (normalized <= lowSanitySoundThreshold)
            {
                if (!heartbeatSource.isPlaying)
                {
                    heartbeatSource.clip = heartbeatClip;
                    heartbeatSource.loop = true;
                    heartbeatSource.Play();
                }

                float intensity = Mathf.InverseLerp(0f, lowSanitySoundThreshold, normalized);
                heartbeatSource.volume = Mathf.Lerp(1f, 0.3f, intensity);
                heartbeatSource.pitch = Mathf.Lerp(1.4f, 0.9f, intensity);
            }
            else
            {
                if (heartbeatSource.isPlaying)
                    heartbeatSource.Stop();
            }
        }

        // ---- Drone / звон в ушах ----
        if (droneSource != null && lowSanityDroneClip != null)
        {
            // Дрон начинает играть на пороге lowSanitySoundThreshold и усиливается к 0
            if (normalized <= lowSanitySoundThreshold)
            {
                if (!droneSource.isPlaying)
                {
                    droneSource.clip = lowSanityDroneClip;
                    droneSource.loop = true;
                    droneSource.Play();
                }

                float intensity = Mathf.InverseLerp(0f, lowSanitySoundThreshold, normalized);
                droneSource.volume = Mathf.Lerp(0.6f, 0.15f, intensity);
                droneSource.pitch = Mathf.Lerp(0.85f, 1.1f, intensity);
            }
            else
            {
                if (droneSource.isPlaying)
                    droneSource.Stop();
            }
        }
    }

    // ---------- Audio: случайные шёпоты ----------
    private void UpdateWhispers()
    {
        if (whisperSource == null || whisperClips == null || whisperClips.Length == 0) return;

        float normalized = startingSanity > 0f ? currentSanity / startingSanity : 0f;

        // Шёпоты только при низком рассудке
        if (normalized > lowSanitySoundThreshold) return;

        whisperTimer += Time.deltaTime;
        if (whisperTimer >= nextWhisperTime)
        {
            whisperTimer = 0f;
            // Чем ниже рассудок — тем чаще шёпоты
            float t = Mathf.InverseLerp(0f, lowSanitySoundThreshold, normalized); // 0 при пустом, 1 при пороге
            float min = Mathf.Lerp(whisperIntervalMin, whisperIntervalMin * 2.5f, t);
            float max = Mathf.Lerp(whisperIntervalMax, whisperIntervalMax * 2.5f, t);
            nextWhisperTime = Random.Range(min, max);

            AudioClip clip = whisperClips[Random.Range(0, whisperClips.Length)];
            if (clip != null)
            {
                float vol = Mathf.Lerp(0.8f, 0.3f, t);
                whisperSource.pitch = Random.Range(0.85f, 1.15f);
                whisperSource.PlayOneShot(clip, vol);
            }
        }
    }

    // ---------- Death ----------
    private void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[Sanity] Рассудок исчерпан — смерть.");

        // Останавливаем звуки
        if (heartbeatSource != null && heartbeatSource.isPlaying) heartbeatSource.Stop();
        if (droneSource != null && droneSource.isPlaying) droneSource.Stop();

        // Событие для других систем
        OnSanityDepleted?.Invoke();

        if (freezeTimeOnDeath)
            Time.timeScale = 0f;

        // Показать экран смерти
        if (deathScreen != null)
            deathScreen.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (deathScreenCanvasGroup != null)
            StartCoroutine(FadeInDeathScreen());
    }

    private IEnumerator FadeInDeathScreen()
    {
        float t = 0f;
        while (t < deathFadeDuration)
        {
            // используем unscaledDeltaTime, чтобы работало при Time.timeScale = 0
            t += Time.unscaledDeltaTime;
            deathScreenCanvasGroup.alpha = Mathf.Clamp01(t / deathFadeDuration);
            yield return null;
        }
        deathScreenCanvasGroup.alpha = 1f;
    }

    // Можно повесить на кнопку "Заново" в экране смерти
    public void RestartGame()
    {
        Time.timeScale = 1f;
        string scene = string.IsNullOrEmpty(restartSceneName)
            ? SceneManager.GetActiveScene().name
            : restartSceneName;
        SceneManager.LoadScene(scene);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Геттеры ----------
    public float CurrentSanity => currentSanity;
    public float MaxSanity => startingSanity;
    public float SanityNormalized => startingSanity > 0f ? currentSanity / startingSanity : 0f;
    public bool IsDepleted => currentSanity <= 0f;
    public bool IsDead => isDead;
}