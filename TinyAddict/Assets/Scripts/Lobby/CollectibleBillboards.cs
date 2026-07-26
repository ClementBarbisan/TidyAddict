using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Affiche un texte flottant au-dessus de chaque objet que VOTRE équipe doit
/// amener dans sa zone (types requis par le ZoneStepPoint de l'étape courante).
/// Le texte disparaît quand l'objet est dans votre zone, et réapparaît s'il en
/// ressort. Purement local (chaque joueur voit les objectifs de SON équipe),
/// s'auto-instancie, aucun setup nécessaire.
/// </summary>
public class CollectibleBillboards : MonoBehaviour
{
    private const float RefreshInterval = 1f;

    private float _nextRefresh;
    private PlayerProfile _localProfile;
    private ZoneStepPoint[] _stepPoints = new ZoneStepPoint[0];
    private CollectionZoneMarker[] _markers = new CollectionZoneMarker[0];
    private readonly Dictionary<ValueCollectible, TextMesh> _billboards =
        new Dictionary<ValueCollectible, TextMesh>(32);

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
            RefreshReferences();
        }

        UpdateBillboards();
    }

    private void RefreshReferences()
    {
        _stepPoints = FindObjectsByType<ZoneStepPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        _markers = FindObjectsByType<CollectionZoneMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

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

        // Un billboard par objet collectable
        foreach (var collectible in FindObjectsByType<ValueCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (_billboards.ContainsKey(collectible))
                continue;

            _billboards[collectible] = CreateBillboard(collectible);
        }
    }

    private void UpdateBillboards()
    {
        var gameState = GameState.Instance;
        Team myTeam = _localProfile != null ? _localProfile.Team : Team.None;

        bool gameRunning = gameState != null && gameState.IsStarted && gameState.IsEnded == false;
        int currentStep = gameRunning ? gameState.CurrentStep : 0;

        // Les types requis par MON équipe à l'étape courante
        ZoneStepPoint myStepPoint = null;
        CollectionZoneMarker myZone = null;

        if (gameRunning && myTeam != Team.None)
        {
            foreach (var point in _stepPoints)
            {
                if (point != null && point.Team == myTeam && point.Step == currentStep)
                {
                    myStepPoint = point;
                    break;
                }
            }

            foreach (var marker in _markers)
            {
                if (marker != null && marker.Team == myTeam)
                {
                    myZone = marker;
                    break;
                }
            }
        }

        var camera = Camera.main;
        List<ValueCollectible> toRemove = null;

        foreach (var pair in _billboards)
        {
            var collectible = pair.Key;
            var billboard = pair.Value;

            // Objet détruit : on nettoie son label
            if (collectible == null)
            {
                if (billboard != null)
                    Destroy(billboard.gameObject);
                (toRemove ??= new List<ValueCollectible>()).Add(collectible);
                continue;
            }

            if (billboard == null)
                continue;

            bool required = myStepPoint != null && myStepPoint.SearchTypeObj(collectible.Type) > 0;
            bool inMyZone = myZone != null && IsInsideZone(collectible.transform.position, myZone);

            // Visible si mon équipe a besoin de cet objet ET qu'il n'est pas
            // (ou plus) dans notre zone — il réapparaît s'il en ressort
            bool visible = required && inMyZone == false;

            if (billboard.gameObject.activeSelf != visible)
                billboard.gameObject.SetActive(visible);

            if (visible == false)
                continue;

            billboard.color = UITheme.PseudoColor(myTeam);

            // Suit l'objet sans hériter de sa rotation (les cubes roulent)
            billboard.transform.position = collectible.transform.position + Vector3.up * 1.4f;

            if (camera != null)
            {
                billboard.transform.rotation = Quaternion.LookRotation(
                    billboard.transform.position - camera.transform.position);
            }
        }

        if (toRemove != null)
        {
            foreach (var key in toRemove)
                _billboards.Remove(key);
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

    private TextMesh CreateBillboard(ValueCollectible collectible)
    {
        // Indépendant de l'objet (pas d'héritage de rotation), suivi dans Update
        var labelObject = new GameObject("ObjectiveLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.position = collectible.transform.position + Vector3.up * 1.4f;

        var text = labelObject.AddComponent<TextMesh>();
        var font = UITheme.BodyExtraBold;
        text.font = font;
        text.fontSize = 44;
        text.characterSize = 0.035f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.text = LabelOf(collectible.Type);
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        labelObject.SetActive(false);
        return text;
    }

    private static string LabelOf(ValueCollectible.TypeObj type)
    {
        switch (type)
        {
            case ValueCollectible.TypeObj.potion: return "POTION";
            case ValueCollectible.TypeObj.potionGreen: return "POTION VERTE";
            case ValueCollectible.TypeObj.potionRed: return "POTION ROUGE";
            case ValueCollectible.TypeObj.potionBlue: return "POTION BLEUE";
            case ValueCollectible.TypeObj.cauldron: return "CHAUDRON";
            case ValueCollectible.TypeObj.cauldronShiny: return "CHAUDRON BRILLANT";
            default: return type.ToString().ToUpperInvariant();
        }
    }
}
