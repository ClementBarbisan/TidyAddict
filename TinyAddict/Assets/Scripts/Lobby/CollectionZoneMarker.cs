using UnityEngine;

/// <summary>
/// Marqueur de zone de collecte : source de vérité de la position ET de la
/// taille de la zone. Génère et maintient lui-même une box mesh translucide
/// aux dimensions exactes du volume de comptage (empreinte × hauteur), dans
/// l'éditeur comme en jeu. Déplacer l'objet déplace la zone ; changer
/// Footprint/Height redimensionne le mesh et le comptage ensemble.
/// </summary>
[ExecuteAlways]
public class CollectionZoneMarker : MonoBehaviour
{
    public Team Team = Team.None;

    [Tooltip("Emprise au sol de la zone (X × Z, en mètres)")]
    [SerializeField] private Vector2 _footprint = new Vector2(8f, 8f);

    [Tooltip("Hauteur de la zone (pour attraper les objets lancés)")]
    [SerializeField] private float _height = 4f;

    [Tooltip("Matériau translucide du mesh (assigné par le setup)")]
    [SerializeField] private Material _visualMaterial;

    private Transform _visual;

    public Vector3 Center => transform.position + Vector3.up * (_height * 0.5f);
    public Vector3 HalfExtents => new Vector3(_footprint.x * 0.5f, _height * 0.5f, _footprint.y * 0.5f);

    private void Update()
    {
        EnsureVisual();

        if (_visual == null)
            return;

        // Le mesh colle toujours exactement au volume de comptage
        _visual.localPosition = new Vector3(0f, _height * 0.5f, 0f);
        _visual.localScale = new Vector3(_footprint.x, _height, _footprint.y);
    }

    private void EnsureVisual()
    {
        if (_visual != null)
            return;

        _visual = transform.Find("Visual");

        if (_visual == null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Visual";
            box.transform.SetParent(transform, false);

            var collider = box.GetComponent<Collider>();
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);

            _visual = box.transform;
        }

        if (_visualMaterial != null)
        {
            var renderer = _visual.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != _visualMaterial)
                renderer.sharedMaterial = _visualMaterial;
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
