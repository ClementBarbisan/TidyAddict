using UnityEngine;

/// <summary>
/// Marqueur de zone de collecte, posé sur le rectangle coloré de la scène.
/// GameState lit position et taille directement ici : déplacer/redimensionner
/// le rectangle dans l'éditeur déplace la vraie zone de comptage.
/// La scène étant identique chez tous les joueurs, pas besoin de réseau.
/// </summary>
public class CollectionZoneMarker : MonoBehaviour
{
    public Team Team = Team.None;

    [Tooltip("Hauteur de la boîte de comptage (pour attraper les objets lancés)")]
    [SerializeField] private float _height = 4f;

    public Vector3 Center => transform.position + Vector3.up * (_height * 0.5f);

    public Vector3 HalfExtents
    {
        get
        {
            // Emprise au sol lue sur le visuel enfant : redimensionner le
            // rectangle redimensionne la zone
            float x = 4f;
            float z = 4f;
            var visual = transform.Find("Visual");
            if (visual != null)
            {
                x = Mathf.Abs(visual.lossyScale.x) * 0.5f;
                z = Mathf.Abs(visual.lossyScale.z) * 0.5f;
            }
            return new Vector3(x, _height * 0.5f, z);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Team == Team.Red
            ? new Color(1f, 0.3f, 0.25f, 0.7f)
            : new Color(0.3f, 0.55f, 1f, 0.7f);
        Gizmos.DrawWireCube(Center, HalfExtents * 2f);
    }
}
