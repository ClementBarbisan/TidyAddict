using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Surligne dans le monde les objets que VOTRE équipe doit ramener (types
/// requis par le ZoneStepPoint de l'étape courante) : teinte pulsante couleur
/// d'équipe sur le mesh. Le surlignage s'éteint pour les objets déjà dans
/// votre zone et pour les types dont le quota est atteint.
/// La liste détaillée est affichée par le MatchHUD (sous le roster).
/// Purement local, s'auto-instancie, aucun setup nécessaire.
/// </summary>
public class CollectibleBillboards : MonoBehaviour
{
    private const float RefreshInterval = 0.3f;

    private float _nextRefresh;
    private PlayerProfile _localProfile;
    private ZoneStepPoint[] _stepPoints = new ZoneStepPoint[0];
    private CollectionZoneMarker _myZone;
    private ZoneStepPoint _myStepPoint;

    private readonly List<ValueCollectible> _collectibles = new List<ValueCollectible>(32);
    private readonly Dictionary<ValueCollectible, Renderer[]> _renderers = new Dictionary<ValueCollectible, Renderer[]>(32);
    private readonly Dictionary<ValueCollectible.TypeObj, int> _presentPerType = new Dictionary<ValueCollectible.TypeObj, int>(8);
    private readonly HashSet<GameObject> _countedObjects = new HashSet<GameObject>();
    private MaterialPropertyBlock _block;
    private MaterialPropertyBlock _clearBlock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("CollectibleBillboards");
        DontDestroyOnLoad(go);
        go.AddComponent<CollectibleBillboards>();
    }

    /// <summary>Objets de ce type actuellement dans MA zone (pour le HUD).</summary>
    public static int CountInMyZone(CollectionZoneMarker zone, ValueCollectible.TypeObj type)
    {
        if (zone == null)
            return 0;

        int count = 0;
        var counted = new HashSet<GameObject>();
        var hits = Physics.OverlapBox(zone.Center, zone.HalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (root.CompareTag("Grabbable") == false || counted.Add(root) == false)
                continue;

            var collectible = root.GetComponent<ValueCollectible>();
            if (collectible != null && collectible.Type == type)
                count++;
        }

        return count;
    }

    public static string LabelOf(ValueCollectible.TypeObj type)
    {
        switch (type)
        {
            case ValueCollectible.TypeObj.potion: return "Potion";
            case ValueCollectible.TypeObj.potionGreen: return "Green Potion";
            case ValueCollectible.TypeObj.potionRed: return "Red Potion";
            case ValueCollectible.TypeObj.potionBlue: return "Blue Potion";
            case ValueCollectible.TypeObj.cauldron: return "Cauldron";
            case ValueCollectible.TypeObj.cauldronRed: return "Black Cauldron";
            case ValueCollectible.TypeObj.cauldronGreen: return "Black Cauldron";
            case ValueCollectible.TypeObj.cauldronBlue: return "Black Cauldron";
            case ValueCollectible.TypeObj.cauldronShiny: return "Shiny Cauldron";
            default: return type.ToString();
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            RefreshReferences();
        }

        UpdateHighlights();
    }

    private void RefreshReferences()
    {
        _stepPoints = FindObjectsByType<ZoneStepPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (_localProfile == null)
        {
            foreach (var profile in FindObjectsByType<PlayerProfile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (profile.Object != null && profile.Object.IsValid && profile.HasInputAuthority)
                {
                    _localProfile = profile;
                    break;
                }
            }
        }

        Team myTeam = _localProfile != null ? _localProfile.Team : Team.None;
        int currentStep = GameState.Instance != null && GameState.Instance.IsStarted ? GameState.Instance.CurrentStep : 0;

        _myStepPoint = null;
        _myZone = null;

        if (myTeam != Team.None)
        {
            foreach (var point in _stepPoints)
            {
                if (point != null && point.Team == myTeam && point.Step == currentStep)
                {
                    _myStepPoint = point;
                    break;
                }
            }

            foreach (var marker in FindObjectsByType<CollectionZoneMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (marker.Team == myTeam)
                {
                    _myZone = marker;
                    break;
                }
            }
        }

        // Inventaire des collectables + compte par type dans ma zone
        _collectibles.Clear();
        _collectibles.AddRange(FindObjectsByType<ValueCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        _presentPerType.Clear();
        _countedObjects.Clear();
        if (_myZone != null)
        {
            var hits = Physics.OverlapBox(_myZone.Center, _myZone.HalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
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
    }

    private void UpdateHighlights()
    {
        _block ??= new MaterialPropertyBlock();
        _clearBlock ??= new MaterialPropertyBlock();

        bool gameRunning = GameState.Instance != null && GameState.Instance.IsStarted && GameState.Instance.IsEnded == false;
        Color teamColor = _localProfile != null ? PlayerProfile.ColorOfTeam(_localProfile.Team) : Color.white;

        // Pulsation de la surbrillance
        float pulse = 0.2f + Mathf.PingPong(Time.time * 0.9f, 0.35f);
        Color tint = Color.Lerp(Color.white, teamColor, pulse);
        _block.Clear();
        _block.SetColor("_BaseColor", tint);

        foreach (var collectible in _collectibles)
        {
            if (collectible == null)
                continue;

            if (_renderers.TryGetValue(collectible, out var renderers) == false)
            {
                renderers = collectible.GetComponentsInChildren<Renderer>();
                _renderers[collectible] = renderers;
            }

            bool highlighted = false;
            if (gameRunning && _myStepPoint != null)
            {
                int needed = _myStepPoint.SearchTypeObj(collectible.Type);
                if (needed > 0)
                {
                    _presentPerType.TryGetValue(collectible.Type, out int present);
                    bool quotaMet = present >= needed;
                    bool inMyZone = _myZone != null && IsInsideZone(collectible.transform.position, _myZone);
                    highlighted = quotaMet == false && inMyZone == false;
                }
            }

            foreach (var itemRenderer in renderers)
            {
                if (itemRenderer == null)
                    continue;
                itemRenderer.SetPropertyBlock(highlighted ? _block : _clearBlock);
            }
        }
    }

    private static bool IsInsideZone(Vector3 position, CollectionZoneMarker zone)
    {
        Vector3 delta = position - zone.Center;
        Vector3 halfExtents = zone.HalfExtents;
        return Mathf.Abs(delta.x) <= halfExtents.x &&
               Mathf.Abs(delta.y) <= halfExtents.y &&
               Mathf.Abs(delta.z) <= halfExtents.z;
    }
}
