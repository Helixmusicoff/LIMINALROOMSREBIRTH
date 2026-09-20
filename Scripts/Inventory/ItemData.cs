using UnityEngine;

public enum ItemType
{
    Weapon,
    Tool,
    Consumable,
    Quest,
    Material,
    Other
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public Sprite icon;
    public GameObject prefab;
    public bool isStackable = false;
    public int maxStackSize = 99;
    public ItemType itemType;
    
    public bool canBeUsed = true;
    public bool destroyAfterUse = true; // НОВОЕ: уничтожать ли предмет после использования
    public float useCooldown = 0f;
    public AudioClip pickupSound;
    public AudioClip dropSound;
    public AudioClip useSound;
    
    public int healAmount = 0;
    public int damageAmount = 0;
    public int healRassudokAmount = 0;
    public int SpeedUp = 0;
    
    public string useAnimationTrigger = "Use";
    public float useAnimationDuration = 0.5f;
    public Vector3 equipPositionOffset = Vector3.zero;  // Смещение позиции
    public Vector3 equipRotationOffset = Vector3.zero;  // Поворот в руке
    
    public virtual void Use()
    {
        Debug.Log($"Using {itemName}");
        
        if (itemType == ItemType.Consumable && healAmount > 0)
        {
            PlayerHealth playerHealth = Object.FindObjectOfType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Heal(healAmount);
                Debug.Log($"Вылечено {healAmount} HP");
                
                Animator animator = playerHealth.GetComponent<Animator>();
                if (animator != null && !string.IsNullOrEmpty(useAnimationTrigger))
                {
                    animator.SetTrigger(useAnimationTrigger);
                }
            }
            
            if (useSound != null)
            {
                AudioSource.PlayClipAtPoint(useSound, Vector3.zero);
            }
        }
        if (itemType == ItemType.Consumable && healRassudokAmount > 0)
        {
            SanityManager SM = Object.FindObjectOfType<SanityManager>();
            if (SM != null)
            {
                SM.currentSanity += healRassudokAmount;
                if (SM.currentSanity + healRassudokAmount > 100)
                {
                    SM.currentSanity = 100;
                }
            }
        }
     
    }
    
    public virtual void OnPickup()
    {
        Debug.Log($"Picked up {itemName}");
    }
    
    public virtual void OnDrop()
    {
        Debug.Log($"Dropped {itemName}");
    }
}