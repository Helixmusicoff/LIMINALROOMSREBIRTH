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

    [Header("Audio - Extra (шёпот, дрон)")]
    [SerializeField] private AudioSource whisperSource;
    [SerializeField] private AudioSource droneSource;
    [SerializeField] private AudioClip[] whisperClips;
    [SerializeField] private AudioClip lowSanityDroneClip;
    [SerializeField] private float whisperIntervalMin = 4f;
    [SerializeField] private float whisperIntervalMax = 10f;

    [Header("Death Screen")]
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private CanvasGroup deathScreenCanvasGroup;
    [SerializeField] private CanvasGroup deathContentCanvasGroup;
    [SerializeField] private float deathFadeDuration = 2f;
    [SerializeField] private float deathContentDelay = 0.6f;
    [SerializeField] private float deathContentFadeDuration = 1f;
    [SerializeField] private bool freezeTimeOnDeath = true;
    [SerializeField] private string restartSceneName = "";

    public float currentSanity;
    private bool isInitialized = false;
    private bool isDead = false;

    private float whisperTimer = 0f;
    private float nextWhisperTime = 0f;

    public static SanityManager Instance { get; private set; }

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

        // Готовим экран смерти: активен, но прозрачен и не перехватывает клики
        if (deathScreen != null && !deathScreen.activeSelf)
            deathScreen.SetActive(true);

        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.alpha = 0f;
            deathScreenCanvasGroup.interactable = false;
            deathScreenCanvasGroup.blocksRaycasts = false;
        }

        if (deathContentCanvasGroup != null)
        {
            deathContentCanvasGroup.alpha = 0f;
            deathContentCanvasGroup.interactable = false;
            deathContentCanvasGroup.blocksRaycasts = false;
        }

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

        // Сбрасываем экран смерти
        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.alpha = 0f;
            deathScreenCanvasGroup.interactable = false;
            deathScreenCanvasGroup.blocksRaycasts = false;
        }

        if (deathContentCanvasGroup != null)
        {
            deathContentCanvasGroup.alpha = 0f;
            deathContentCanvasGroup.interactable = false;
            deathContentCanvasGroup.blocksRaycasts = false;
        }

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

        // Heartbeat
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

        // Drone / звон в ушах
        if (droneSource != null && lowSanityDroneClip != null)
        {
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

        if (normalized > lowSanitySoundThreshold) return;

        whisperTimer += Time.deltaTime;
        if (whisperTimer >= nextWhisperTime)
        {
            whisperTimer = 0f;
            float t = Mathf.InverseLerp(0f, lowSanitySoundThreshold, normalized);
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

        if (heartbeatSource != null && heartbeatSource.isPlaying) heartbeatSource.Stop();
        if (droneSource != null && droneSource.isPlaying) droneSource.Stop();

        OnSanityDepleted?.Invoke();

        if (freezeTimeOnDeath)
            Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (deathScreen != null && !deathScreen.activeSelf)
            deathScreen.SetActive(true);

        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.alpha = 0f;
            deathScreenCanvasGroup.blocksRaycasts = true;
        }

        if (deathContentCanvasGroup != null)
        {
            deathContentCanvasGroup.alpha = 0f;
            deathContentCanvasGroup.interactable = false;
            deathContentCanvasGroup.blocksRaycasts = false;
        }

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // 1. Плавное затемнение фона
        if (deathScreenCanvasGroup != null)
        {
            float t = 0f;
            while (t < deathFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                deathScreenCanvasGroup.alpha = Mathf.Clamp01(t / deathFadeDuration);
                yield return null;
            }
            deathScreenCanvasGroup.alpha = 1f;
        }

        // 2. Задержка перед появлением текста
        if (deathContentDelay > 0f)
        {
            float t = 0f;
            while (t < deathContentDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 3. Плавное появление текста и кнопок
        if (deathContentCanvasGroup != null)
        {
            float t = 0f;
            while (t < deathContentFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                deathContentCanvasGroup.alpha = Mathf.Clamp01(t / deathContentFadeDuration);
                yield return null;
            }
            deathContentCanvasGroup.alpha = 1f;
            deathContentCanvasGroup.interactable = true;
            deathContentCanvasGroup.blocksRaycasts = true;
        }
    }

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