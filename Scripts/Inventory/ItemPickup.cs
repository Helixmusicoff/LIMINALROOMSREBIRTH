using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public ItemData itemData;
    private bool isInRange = false;
    
    // Опционально: визуальная подсказка
    public GameObject pickupHintPrefab;
    private GameObject currentHint;
    
    private void Start()
    {
        // Автоматически настраиваем коллайдер
        SetupCollider();
        
        // Автоматически настраиваем Rigidbody
        SetupRigidbody();
        
        // Проверяем наличие ItemData
        if (itemData == null)
        {
            Debug.LogWarning($"ItemPickup на {gameObject.name}: itemData не назначен!");
        }
        else
        {
            Debug.Log($"Предмет {itemData.itemName} готов к подбору");
        }
    }
    
    private void SetupCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 1.2f;
            Debug.Log($"Добавлен SphereCollider для {gameObject.name}");
        }
        else
        {
            col.isTrigger = true;
        }
    }
    
    private void SetupRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 1f;
            rb.linearDamping = 1f;
            Debug.Log($"Добавлен Rigidbody для {gameObject.name}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInRange = true;
            ShowPickupHint(true);
            Debug.Log($"Игрок подошел к {itemData?.itemName ?? gameObject.name}. Нажмите F для подбора");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInRange = false;
            ShowPickupHint(false);
            Debug.Log($"Игрок отошел от {itemData?.itemName ?? gameObject.name}");
        }
    }
    
    private void ShowPickupHint(bool show)
    {
        if (pickupHintPrefab != null)
        {
            if (show && currentHint == null)
            {
                currentHint = Instantiate(pickupHintPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
            }
            else if (!show && currentHint != null)
            {
                Destroy(currentHint);
                currentHint = null;
            }
        }
    }
    
    public void PickUp()
    {
        Debug.Log($"PickUp() вызван для {itemData?.itemName ?? gameObject.name}");
        
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("InventoryManager.Instance не найден!");
            return;
        }
        
        if (itemData == null)
        {
            Debug.LogError($"ItemData не назначен на {gameObject.name}!");
            return;
        }
        
        // Пытаемся добавить в инвентарь
        if (InventoryManager.Instance.AddItem(itemData))
        {
            // Воспроизводим звук
            if (itemData.pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(itemData.pickupSound, transform.position, 1f);
            }
            
            // Вызываем событие подбора
            itemData.OnPickup();
            
            // Уничтожаем предмет
            Destroy(gameObject);
            Debug.Log($"Предмет {itemData.itemName} подобран и уничтожен");
        }
        else
        {
            Debug.Log("Не удалось добавить предмет в инвентарь (возможно, инвентарь полон)");
        }
    }
}