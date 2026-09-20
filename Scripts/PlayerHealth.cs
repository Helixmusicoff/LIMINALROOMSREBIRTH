using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Настройки здоровья")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("События")]
    public UnityEvent<int, int> onHealthChanged;
    public UnityEvent onPlayerDeath;

    void Start()
    {
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return; // Не наносим урон мёртвому игроку
        
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(int amount)
    {
        if (currentHealth <= 0) return;
        
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        onPlayerDeath?.Invoke();
        SceneManager.LoadScene(3); // Сцена поражения
    }
    
    public bool IsDead()
    {
        return currentHealth <= 0;
    }
    
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
    
    public int GetMaxHealth()
    {
        return maxHealth;
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
    if (scene.name != "MainMenu" && scene.name != "Loading_screen")
    {
        currentHealth = maxHealth;
        Debug.Log($"Health reset to {currentHealth}");
    }
}

}