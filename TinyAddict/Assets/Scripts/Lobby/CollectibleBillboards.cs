using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Liste flottante des objets à ramener, affichée AU-DESSUS de chaque zone de
/// collecte (exigences du ZoneStepPoint de l'équipe à l'étape courante).
/// Chaque ligne montre la progression (« Potion verte 1/2 ») et DISPARAÎT
/// quand le quota est atteint — elle réapparaît si un objet ressort de la
/// zone. Suit la zone quand elle change d'étape. Purement local,
/// s'auto-instancie, aucun setup nécessaire.
/// </summary>
public class CollectibleBillboards : MonoBehaviour
{
    private const float RefreshInterval = 0.3f;

    private float _nextRefresh;
    private ZoneStepPoint[] _stepPoints = new ZoneStepPoint[0];
    private readonly Dictionary<CollectionZoneMarker, TextMesh> _billboards =
        new Dictionary<CollectionZoneMarker, TextMesh>(4);

    private readonly StringBuilder _builder = new StringBuilder(256);
    private readonly Dictionary<ValueCollectible.TypeObj, int> _presentPerType =
        new Dictionary<ValueCollectible.TypeObj, int>(8);
    private readonly HashSet<GameObject> _countedObjects = new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("CollectibleBillboards");
        DontDestroyOnLoad(go);
        go.AddComponent<CollectibleBillboards>();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            RefreshBillboards();
        }

        // Face caméra en continu
        var camera = Camera.main;
        if (camera == null)
            return;

        foreach (var billboard in _billboards.Values)
        {
            if (billboard != null && billboard.gameObject.activeSelf)
            {
                billboard.transform.rotation = Quaternion.LookRotation(
                    billboard.transform.position - camera.transform.position);
            }
        }
    }

    private void RefreshBillboards()
    {
        if (_stepPoints.Length == 0)
            _stepPoints = FindObjectsByType<ZoneStepPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int currentStep = GameState.Instance != null && GameState.Instance.IsStarted
            ? GameState.Instance.CurrentStep
            : 0;

        foreach (var marker in FindObjectsByType<CollectionZoneMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (_billboards.TryGetValue(marker, out var billboard) == false || billboard == null)
            {
                billboard = CreateBillboard(marker);
                _billboards[marker] = billboard;
            }

            UpdateZoneBillboard(marker, billboard, currentStep);
        }
    }

    private void UpdateZoneBillboard(CollectionZoneMarker marker, TextMesh billboard, int step)
    {
        // Les exigences de CETTE équipe à CETTE étape
        ZoneStepPoint stepPoint = null;
        foreach (var point in _stepPoints)
        {
            if (point != null && point.Team == marker.Team && point.Step == step)
            {
                stepPoint = point;
                break;
            }
        }

        if (stepPoint == null || stepPoint.ListObj.Count == 0)
        {
            billboard.gameObject.SetActive(false);
            return;
        }

        CountPresentTypes(marker);

        // Une ligne par type NON complété ; les quotas atteints disparaissent
        _builder.Length = 0;
        foreach (var requirement in stepPoint.ListObj)
        {
            if (requirement.NbObj <= 0)
                continue;

            _presentPerType.TryGetValue(requirement.Type, out int present);
            if (present >= requirement.NbObj)
                continue;

            if (_builder.Length > 0)
                _builder.Append('\n');
            _builder.Append($"{LabelOf(requirement.Type)}  {present}/{requirement.NbObj}");
        }

        if (_builder.Length == 0)
        {
            // Tout est ramené : plus rien à afficher
            billboard.gameObject.SetActive(false);
            return;
        }

        billboard.gameObject.SetActive(true);
        billboard.text = "À RAMENER\n" + _builder;
        billboard.color = UITheme.PseudoColor(marker.Team);
    }

    // Compte les objets de chaque type actuellement dans la zone
    private void CountPresentTypes(CollectionZoneMarker marker)
    {
        _presentPerType.Clear();
        _countedObjects.Clear();

        var hits = Physics.OverlapBox(marker.Center, marker.HalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (root.CompareTag("Grabbable") == false || _countedObjects.Add(root) == false)
                continue;

            var collectible = root.GetComponent<ValueCollectible>();
            if (collectible == null)
                continue;

            _presentPerType.TryGetValue(collectible.Type, out int count);
            _presentPerType[collectible.Type] = count + 1;
        }
    }

    private TextMesh CreateBillboard(CollectionZoneMarker marker)
    {
        // Enfant de la zone : suit ses sauts d'étape automatiquement
        var labelObject = new GameObject("ObjectivesList");
        labelObject.transform.SetParent(marker.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, marker.HalfExtents.y * 2f + 2.2f, 0f);

        var text = labelObject.AddComponent<TextMesh>();
        var font = UITheme.BodyExtraBold;
        text.font = font;
        text.fontSize = 48;
        text.characterSize = 0.045f;
        text.anchor = TextAnchor.LowerCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.lineSpacing = 1.1f;
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        labelObject.SetActive(false);
        return text;
    }

    private static string LabelOf(ValueCollectible.TypeObj type)
    {
        switch (type)
        {
            case ValueCollectible.TypeObj.potion: return "Potion";
            case ValueCollectible.TypeObj.potionGreen: return "Potion verte";
            case ValueCollectible.TypeObj.potionRed: return "Potion rouge";
            case ValueCollectible.TypeObj.potionBlue: return "Potion bleue";
            case ValueCollectible.TypeObj.cauldron: return "Chaudron";
            case ValueCollectible.TypeObj.cauldronBlack: return "Chaudron noir";
            case ValueCollectible.TypeObj.cauldronShiny: return "Chaudron brillant";
            default: return type.ToString();
        }
    }
}
