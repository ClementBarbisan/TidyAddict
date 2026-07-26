using UnityEngine;

public class ValueCollectible : MonoBehaviour
{
   public enum TypeObj
   {
      potion,
      potionGreen,
      potionRed,
      potionBlue,
      cauldron,
      cauldronShiny
   }
   public int Value = 1;
   public TypeObj Type;
}
