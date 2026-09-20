using UnityEngine;

public class RassudokHeal : MonoBehaviour
{
   public float amount;
   private SanityManager babos;
   private void Update()
   {
    if (Input.GetKeyDown(KeyCode.Mouse0))
    {
        babos.currentSanity += amount;
        if (babos.currentSanity + amount > 100)
        {
            babos.currentSanity = 100;
        }   
    }
   }
   
}