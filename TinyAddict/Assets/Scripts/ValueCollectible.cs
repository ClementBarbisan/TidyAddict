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
      cauldronBlack,
      cauldronShiny
   }
   public int Value = 1;
   public TypeObj Type;
}
