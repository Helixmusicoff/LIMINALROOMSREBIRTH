using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SanityManager : MonoBehaviour
{
    [Header("Настройки рассудка")]
    [SerializeField] private float startingSanity = 100f; // Начальный рассудок
    [SerializeField] private float sanityDrainPerSecond = 1f; // Потеря в секунду

    [Header("UI компоненты")]
    [SerializeField] private Slider sanitySlider; // Слайдер рассудка

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

    private void UpdateUI()
    {
        if (sanitySlider != null)
        {
            // Проверяем, что слайдер существует и обновляем значение
            sanitySlider.maxValue = startingSanity;
            sanitySlider.value = currentSanity;
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
            
            // Поиск слайдера в новой сцене, если ссылка потерялась
            if (sanitySlider == null)
            {
                sanitySlider = FindObjectOfType<Slider>();
                if (sanitySlider != null)
                {
                    Debug.Log("Sanity slider found in new scene");
                    UpdateUI();
                }
            }
        }
    }

    // Геттеры для других скриптов
    public float CurrentSanity => currentSanity;
    public float MaxSanity => startingSanity;
    public bool IsGameOver => isGameOver;
}