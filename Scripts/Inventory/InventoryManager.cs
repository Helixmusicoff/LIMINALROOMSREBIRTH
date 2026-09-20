using UnityEngine;
using System.Collections.Generic;

// ============= ОПРЕДЕЛЕНИЯ КЛАССОВ ДОЛЖНЫ БЫТЬ В НАЧАЛЕ =============

[System.Serializable]
public class InventorySlot
{
    public ItemData itemData;
    public int quantity;
    
    public bool IsEmpty()
    {
        return itemData == null;
    }
    
    public void Clear()
    {
        itemData = null;
        quantity = 0;
    }
}

[System.Serializable]
public class SlotSaveData
{
    public bool hasItem;
    public string itemName;
    public int quantity;
}

[System.Serializable]
public class InventorySaveData
{
    public List<SlotSaveData> slotItems;
}

// ============= ОСНОВНОЙ КЛАСС InventoryManager =============

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    
    [SerializeField] private int maxSlots = 3;
    public InventorySlot[] slots;
    private int currentSlotIndex = 0;
    
    [SerializeField] private Transform dropPoint;
    [SerializeField] private float dropForce = 5f;
    [SerializeField] private float pickupRange = 2.5f;
    
    [Header("Визуальное отображение")]
    [SerializeField] private EquipmentVisual equipmentVisual;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        InitializeSlots();
        
        if (equipmentVisual == null)
        {
            equipmentVisual = GetComponent<EquipmentVisual>();
            if (equipmentVisual == null)
            {
                equipmentVisual = GetComponentInChildren<EquipmentVisual>();
            }
        }
    }
    
    private void InitializeSlots()
    {
        slots = new InventorySlot[maxSlots];
        for (int i = 0; i < maxSlots; i++)
        {
            slots[i] = new InventorySlot();
        }
    }
    
    private void Update()
    {
        HandleSlotSelection();
        HandlePickup();
        HandleDrop();
        HandleUseItem();
    }
    
    private void HandleSlotSelection()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            SelectSlot(2);
    }
    
    private void SelectSlot(int index)
    {
        currentSlotIndex = index;
        UpdateEquipmentVisual();
        Debug.Log($"Выбран слот {index + 1}");
    }
    
    private void UpdateEquipmentVisual()
{
    if (equipmentVisual == null) return;
    
    ItemData currentItem = GetCurrentItem();
    if (currentItem != null && currentItem.prefab != null)
    {
        // Передаём и префаб, и данные предмета
        equipmentVisual.EquipItem(currentItem.prefab, currentItem);
    }
    else
    {
        equipmentVisual.UnequipItem();
    }
}
    
    private void HandlePickup()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
            ItemPickup closestItem = null;
            float closestDistance = pickupRange + 1;
            
            foreach (var collider in colliders)
            {
                ItemPickup item = collider.GetComponent<ItemPickup>();
                if (item != null && item.gameObject.activeSelf)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestItem = item;
                    }
                }
            }
            
            if (closestItem != null)
            {
                closestItem.PickUp();
            }
        }
    }
    
    private void HandleDrop()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            DropItem(currentSlotIndex);
        }
    }
    
    private void HandleUseItem()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            UseCurrentItem();
        }
    }
    
    private void UseCurrentItem()
    {
        ItemData currentItem = GetCurrentItem();
        if (currentItem == null)
        {
            Debug.Log("В руке ничего нет");
            return;
        }
        
        if (!currentItem.canBeUsed)
        {
            Debug.Log($"{currentItem.itemName} нельзя использовать");
            return;
        }
        
        currentItem.Use();
        
        if (slots[currentSlotIndex].quantity > 1)
        {
            slots[currentSlotIndex].quantity--;
            InventoryUI.Instance?.UpdateSlot(currentSlotIndex, currentItem);
        }
        else
        {
            slots[currentSlotIndex].Clear();
            InventoryUI.Instance?.UpdateSlot(currentSlotIndex, null);
            UpdateEquipmentVisual();
        }
    }
    
    public bool AddItem(ItemData itemData)
    {
        if (itemData == null) return false;
        
        if (itemData.isStackable)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty() && slots[i].itemData == itemData && slots[i].quantity < itemData.maxStackSize)
                {
                    slots[i].quantity++;
                    InventoryUI.Instance?.UpdateSlot(i, itemData);
                    if (i == currentSlotIndex) UpdateEquipmentVisual();
                    return true;
                }
            }
        }
        
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty())
            {
                slots[i].itemData = itemData;
                slots[i].quantity = 1;
                InventoryUI.Instance?.UpdateSlot(i, itemData);
                if (i == currentSlotIndex) UpdateEquipmentVisual();
                return true;
            }
        }
        
        return false;
    }
    
    private void DropItem(int slotIndex)
{
    if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex].IsEmpty())
    {
        Debug.Log("Нет предмета для выброса");
        return;
    }
    
    ItemData itemToDrop = slots[slotIndex].itemData;
    GameObject objectToDrop = null;
    
    if (slotIndex == currentSlotIndex && equipmentVisual != null)
    {
        objectToDrop = equipmentVisual.DropEquippedItem();
    }
    
    if (objectToDrop == null && itemToDrop.prefab != null)
    {
        Vector3 dropPos = dropPoint ? dropPoint.position : transform.position + transform.forward * 1.5f;
        objectToDrop = Instantiate(itemToDrop.prefab, dropPos, Quaternion.identity);
    }
    
    if (objectToDrop != null)
    {
        ItemPickup pickup = objectToDrop.GetComponent<ItemPickup>();
        if (pickup == null) pickup = objectToDrop.AddComponent<ItemPickup>();
        pickup.itemData = itemToDrop;
        
        SetupDropCollider(objectToDrop);
        
        Rigidbody rb = objectToDrop.GetComponent<Rigidbody>();
        if (rb == null) rb = objectToDrop.AddComponent<Rigidbody>();
        
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 1f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        Vector3 dropDirection = (transform.forward + Vector3.up * 0.5f).normalized;
        rb.AddForce(dropDirection * dropForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);
        
        Debug.Log($"Выброшен предмет: {itemToDrop.itemName}");
    }
    
    // ============= ЭТО САМОЕ ВАЖНОЕ - ОЧИСТКА ИНВЕНТАРЯ =============
    if (slots[slotIndex].quantity > 1)
    {
        slots[slotIndex].quantity--;
        InventoryUI.Instance?.UpdateSlot(slotIndex, itemToDrop);
    }
    else
    {
        slots[slotIndex].Clear();
        InventoryUI.Instance?.UpdateSlot(slotIndex, null);
    }
    
    if (slotIndex == currentSlotIndex)
    {
        UpdateEquipmentVisual();
    }
}
private void SetupDropCollider(GameObject obj)
{
    // Сначала проверяем, есть ли уже коллайдер
    Collider existingCollider = obj.GetComponent<Collider>();
    if (existingCollider != null)
    {
        // Включаем коллайдер и настраиваем его
        existingCollider.enabled = true;
        existingCollider.isTrigger = false; // Важно: не триггер, иначе будет проваливаться
        
        // Если это MeshCollider, его нужно настроить особо
        MeshCollider meshCollider = existingCollider as MeshCollider;
        if (meshCollider != null)
        {
            meshCollider.convex = true; // Обязательно для MeshCollider, участвующего в физике
            meshCollider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning;
        }
        
        return;
    }
    
    // Если коллайдера нет, пробуем найти в детях
    Collider childCollider = obj.GetComponentInChildren<Collider>();
    if (childCollider != null)
    {
        childCollider.enabled = true;
        childCollider.isTrigger = false;
        return;
    }
    
    // Если коллайдера нет нигде, добавляем BoxCollider
    BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
    boxCollider.isTrigger = false;
    
    // Автоматически вычисляем размеры бокса на основе mesh или renderer
    MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
    if (renderer != null)
    {
        boxCollider.size = renderer.bounds.size;
        boxCollider.center = renderer.bounds.center - obj.transform.position;
    }
    else
    {
        // Ищем рендерер в детях
        Renderer childRenderer = obj.GetComponentInChildren<Renderer>();
        if (childRenderer != null)
        {
            boxCollider.size = childRenderer.bounds.size;
            boxCollider.center = childRenderer.bounds.center - obj.transform.position;
        }
        else
        {
            // Если нет меша, стандартный размер
            boxCollider.size = new Vector3(0.5f, 0.5f, 0.5f);
        }
    }
    
    Debug.Log($"Добавлен BoxCollider для {obj.name}");

}
    public ItemData GetCurrentItem()
    {
        if (currentSlotIndex >= 0 && currentSlotIndex < slots.Length && !slots[currentSlotIndex].IsEmpty())
        {
            return slots[currentSlotIndex].itemData;
        }
        return null;
    }
    
    public int GetCurrentSlot()
    {
        return currentSlotIndex;
    }
    
    public InventorySaveData GetSaveData()
    {
        InventorySaveData saveData = new InventorySaveData();
        saveData.slotItems = new List<SlotSaveData>();
        
        foreach (var slot in slots)
        {
            SlotSaveData slotData = new SlotSaveData();
            slotData.hasItem = !slot.IsEmpty();
            if (!slot.IsEmpty())
            {
                slotData.itemName = slot.itemData.itemName;
                slotData.quantity = slot.quantity;
            }
            saveData.slotItems.Add(slotData);
        }
        
        return saveData;
    }
    
    public void LoadSaveData(InventorySaveData saveData)
    {
        if (saveData == null || saveData.slotItems == null) return;
        
        for (int i = 0; i < slots.Length && i < saveData.slotItems.Count; i++)
        {
            if (saveData.slotItems[i].hasItem)
            {
                ItemData itemData = Resources.Load<ItemData>($"Items/{saveData.slotItems[i].itemName}");
                if (itemData != null)
                {
                    slots[i].itemData = itemData;
                    slots[i].quantity = saveData.slotItems[i].quantity;
                }
            }
            else
            {
                slots[i].Clear();
            }
            InventoryUI.Instance?.UpdateSlot(i, slots[i].itemData);
        }
        
        UpdateEquipmentVisual();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}