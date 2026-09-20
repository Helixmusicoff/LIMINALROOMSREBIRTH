using UnityEngine;

public class EquipmentVisual : MonoBehaviour
{
    [Header("Настройки отображения")]
    [SerializeField] private Transform handSlot;
    
    private GameObject currentEquippedItem;
    private ItemData currentItemData;
    
    private void Start()
    {
        if (handSlot == null)
        {
            FindHandSlot();
        }
    }
    
    private void FindHandSlot()
    {
        Transform rightHand = transform.Find("RightHand");
        if (rightHand != null)
        {
            handSlot = rightHand;
            return;
        }
        
        foreach (Transform child in transform.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains("Hand") || child.name.Contains("hand"))
            {
                handSlot = child;
                return;
            }
        }
        
        GameObject handPoint = new GameObject("HandSlot");
        handPoint.transform.SetParent(transform);
        handPoint.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
        handSlot = handPoint.transform;
    }
    
    public void EquipItem(GameObject itemPrefab, ItemData itemData = null)
    {
        UnequipItem();
        
        if (itemPrefab == null) return;
        
        currentItemData = itemData;
        currentEquippedItem = Instantiate(itemPrefab, handSlot);
        
        // Применяем позицию и поворот из ItemData
        if (itemData != null)
        {
            currentEquippedItem.transform.localPosition = itemData.equipPositionOffset;
            currentEquippedItem.transform.localRotation = Quaternion.Euler(itemData.equipRotationOffset);
        }
        else
        {
            currentEquippedItem.transform.localPosition = Vector3.zero;
            currentEquippedItem.transform.localRotation = Quaternion.identity;
        }
        
        // Отключаем коллайдеры
        Collider[] colliders = currentEquippedItem.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        
        // Отключаем физику
        Rigidbody rb = currentEquippedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }
    
    public void UnequipItem()
    {
        if (currentEquippedItem != null)
        {
            Destroy(currentEquippedItem);
            currentEquippedItem = null;
            currentItemData = null;
        }
    }
    
    public GameObject DropEquippedItem()
    {
        if (currentEquippedItem == null) return null;
        
        GameObject droppedItem = currentEquippedItem;
        ItemData droppedItemData = currentItemData;
        
        currentEquippedItem = null;
        currentItemData = null;
        
        droppedItem.transform.SetParent(null);
        
        Collider[] colliders = droppedItem.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            AddDefaultCollider(droppedItem);
        }
        else
        {
            foreach (Collider col in colliders)
            {
                col.enabled = true;
                col.isTrigger = false;
                
                MeshCollider meshCol = col as MeshCollider;
                if (meshCol != null)
                {
                    meshCol.convex = true;
                }
            }
        }
        
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = droppedItem.AddComponent<Rigidbody>();
        }
        
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 1f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        ItemPickup pickup = droppedItem.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            pickup = droppedItem.AddComponent<ItemPickup>();
        }
        pickup.itemData = droppedItemData;
        
        return droppedItem;
    }
    
    private void AddDefaultCollider(GameObject obj)
    {
        BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
        
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = obj.GetComponentInChildren<Renderer>();
        }
        
        if (renderer != null)
        {
            boxCollider.center = renderer.bounds.center - obj.transform.position;
            boxCollider.size = renderer.bounds.size;
        }
        else
        {
            boxCollider.size = new Vector3(0.5f, 0.5f, 0.5f);
        }
        
        boxCollider.isTrigger = false;
    }
    
    public GameObject GetCurrentEquippedItem()
    {
        return currentEquippedItem;
    }
}