using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>
/// État global de la partie, spawné par le serveur au démarrage de la session.
/// - Lobby : la partie est lancée par l'hôte quand assez de joueurs sont là.
/// - Match de 5 min : chaque équipe charge une JAUGE (0 → 100 %) en gardant
///   des objets « Grabbable » dans sa zone : plus il y a d'objets dedans, plus
///   ça charge vite (réglage par défaut : 5 objets pendant 1 min = +25 %).
///   Les zones SAUTENT d'étape en étape chaque minute (ZoneStepPoint).
///   Victoire : première jauge à 100 %, sinon la plus haute à la fin du chrono.
/// </summary>
public class GameState : NetworkBehaviour
{
    public struct MyDataTuple : INetworkStruct
    {
        public int Count;
        public int Total;

        // Constructeur pratique pour l'utiliser comme un Tuple
        public MyDataTuple(int count, int total)
        {
            Count = count;
            Total = total;
        }
    }
    public static GameState Instance { get; private set; }

    [SerializeField] private int _requiredPlayers = 4;
    [SerializeField] private float _matchSeconds = 300f;
    [SerializeField] private float _launchCountdownSeconds = 5f;
    [SerializeField] private float _returnToLobbySeconds = 15f;
    // Position de départ + 2 changements = 3 étapes sur les 5 minutes
    [SerializeField] private int _zoneSteps = 3;

    [Tooltip("Charge apportée par UN objet resté UNE minute dans la zone (0.05 = 5 % → 5 objets × 1 min = 25 %)")]
    [SerializeField] private float _chargePerObjectPerMinute = 0.05f;

    [Tooltip("Son joué chez tous quand les zones changent d'étape")]
    [SerializeField] private AudioClip _zoneMoveClip;

    // Position/taille de secours si aucun CollectionZoneMarker n'est trouvé
    [SerializeField] private Vector3 _redZoneCenter = new Vector3(-12f, 1f, 0f);
    [SerializeField] private Vector3 _blueZoneCenter = new Vector3(12f, 1f, 0f);
    [SerializeField] private Vector3 _zoneHalfExtents = new Vector3(4f, 2f, 4f);

    private CollectionZoneMarker _redZoneMarker;
    private CollectionZoneMarker _blueZoneMarker;
    private ZoneStepPoint[] _stepPoints;
    private int _appliedStep = -1;
    private int _lastConsumedStep;   // serveur : dernière étape dont les objets ont été consommés

    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool GameEnded { get; set; }
    [Networked] public NetworkBool LaunchCountdownStarted { get; set; }
    [Networked] public TickTimer LaunchTimer { get; set; }
    [Networked] public TickTimer LobbyReturnTimer { get; set; }
    [Networked] public int WinnerTeam { get; set; }        // 0 = égalité, 1 = rouge, 2 = bleu
    [Networked] public TickTimer MatchTimer { get; set; }
    [Networked] public float RedCharge { get; set; }
    [Networked] public float BlueCharge { get; set; }
    [Networked] public MyDataTuple RedCollected { get; set; }
    [Networked] public MyDataTuple BlueCollected { get; set; }

    public int RequiredPlayers => _requiredPlayers;
    public int MaxPlayersPerTeam => Mathf.Max(1, _requiredPlayers / 2);

    public bool IsStarted => Object != null && Object.IsValid && GameStarted;
    public bool IsEnded => Object != null && Object.IsValid && GameEnded;

    /// <summary>Compte à rebours de lancement en cours (entre le clic de l'hôte et le vrai départ).</summary>
    public bool IsLaunching => Object != null && Object.IsValid && LaunchCountdownStarted && GameStarted == false;

    public float LaunchRemaining
    {
        get
        {
            if (Object == null || Object.IsValid == false || LaunchTimer.IsRunning == false)
                return 0f;
            return LaunchTimer.RemainingTime(Runner) ?? 0f;
        }
    }

    public float LobbyReturnRemaining
    {
        get
        {
            if (Object == null || Object.IsValid == false || LobbyReturnTimer.IsRunning == false)
                return 0f;
            return LobbyReturnTimer.RemainingTime(Runner) ?? 0f;
        }
    }
    public bool CanStart => ConnectedPlayers >= _requiredPlayers;

    // Jauges 0..1 affichées par le HUD
    public float RedPercent => Object != null && Object.IsValid ? RedCharge : 0f;
    public float BluePercent => Object != null && Object.IsValid ? BlueCharge : 0f;

    public int ZoneStepsCount => _zoneSteps;
    public float StepSeconds => _matchSeconds / Mathf.Max(1, _zoneSteps);
    public float ElapsedSeconds => IsStarted ? Mathf.Max(0f, _matchSeconds - RemainingSeconds) : 0f;
    public int CurrentStep => Mathf.Clamp((int)(ElapsedSeconds / StepSeconds), 0, _zoneSteps - 1);

    public float RemainingSeconds
    {
        get
        {
            if (Object == null || Object.IsValid == false || MatchTimer.IsRunning == false)
                return _matchSeconds;
            return MatchTimer.RemainingTime(Runner) ?? 0f;
        }
    }

    public int ConnectedPlayers
    {
        get
        {
            if (Object == null || Object.IsValid == false)
                return 0;

            int count = 0;
            foreach (var _ in Runner.ActivePlayers)
                count++;
            return count;
        }
    }

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Appelé côté hôte uniquement (state authority) : arme le compte à rebours de lancement.</summary>
    public void StartGame()
    {
        if (Object.HasStateAuthority == false || GameStarted || LaunchCountdownStarted)
            return;

        LaunchCountdownStarted = true;
        LaunchTimer = TickTimer.CreateFromSeconds(Runner, _launchCountdownSeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
            return;

        // Écran de victoire : après le délai, tout le monde retourne au lobby
        if (GameEnded)
        {
            if (LobbyReturnTimer.Expired(Runner))
                ResetToLobby();
            return;
        }

        // Compte à rebours de lancement : à expiration, la partie démarre
        // vraiment et chaque équipe est téléportée sur ses spawns, face à face
        if (GameStarted == false)
        {
            if (LaunchCountdownStarted && LaunchTimer.Expired(Runner))
            {
                GameStarted = true;
                MatchTimer = TickTimer.CreateFromSeconds(Runner, _matchSeconds);
                _lastConsumedStep = 0;
                TeleportPlayersToTeamSpawns();
            }
            return;
        }

        // Changement d'étape : les objets restés dans les zones disparaissent
        // avec elles (livraison consommée), avant que les zones ne sautent
        if (CurrentStep != _lastConsumedStep)
        {
            ConsumeObjectsInZone(Team.Red, _lastConsumedStep);
            ConsumeObjectsInZone(Team.Blue, _lastConsumedStep);
            _lastConsumedStep = CurrentStep;
        }

        // Comptage des zones ~4 fois par seconde, inutile de le faire à chaque tick
        if (Runner.Tick % 16 == 0)
        {
            RefreshZoneMarkers();
            RedCollected = CountCollectiblesInZone(_redZoneMarker, _redZoneCenter, Team.Red, CurrentStep);
            BlueCollected = CountCollectiblesInZone(_blueZoneMarker, _blueZoneCenter, Team.Blue, CurrentStep);
        }

        // Les jauges chargent en continu : chaque objet présent dans la zone
        // apporte _chargePerObjectPerMinute par minute
        float chargePerObjectPerSecond = _chargePerObjectPerMinute / 60f;
        RedCharge = Mathf.Clamp01(RedCharge + RedCollected.Total * chargePerObjectPerSecond * Runner.DeltaTime);
        BlueCharge = Mathf.Clamp01(BlueCharge + BlueCollected.Total * chargePerObjectPerSecond * Runner.DeltaTime);

        // Victoire immédiate à 100 %
        bool redWinsNow = RedCharge >= 1f;
        bool blueWinsNow = BlueCharge >= 1f;

        if (redWinsNow || blueWinsNow || MatchTimer.Expired(Runner))
        {
            GameEnded = true;
            LobbyReturnTimer = TickTimer.CreateFromSeconds(Runner, _returnToLobbySeconds);

            if (RedCharge > BlueCharge)
                WinnerTeam = (int)Team.Red;
            else if (BlueCharge > RedCharge)
                WinnerTeam = (int)Team.Blue;
            else
                WinnerTeam = 0;
        }
    }

    // Remet la partie à zéro : retour à la salle d'attente pour tout le monde,
    // équipes conservées, l'hôte peut relancer
    private void ResetToLobby()
    {
        GameEnded = false;
        GameStarted = false;
        LaunchCountdownStarted = false;
        WinnerTeam = 0;
        RedCharge = 0f;
        BlueCharge = 0f;
        RedCollected = new MyDataTuple(0, 0);
        BlueCollected = new MyDataTuple(0, 0);
        MatchTimer = default;
        LaunchTimer = default;
        LobbyReturnTimer = default;
        _lastConsumedStep = 0;

        // Purge des effets de sorts encore actifs sur les joueurs
        foreach (var effects in FindObjectsByType<PlayerSpellEffects>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (effects.Object != null && effects.Object.IsValid)
                effects.ClearAllEffects();
        }
    }

    public override void Render()
    {
        MoveZonesToCurrentStep();
    }

    // Téléporte chaque joueur sur un point de spawn de son équipe, tourné vers
    // le camp adverse. Serveur uniquement, au vrai départ de la partie.
    private void TeleportPlayersToTeamSpawns()
    {
        var redPoints = new List<TeamSpawnPoint>();
        var bluePoints = new List<TeamSpawnPoint>();

        foreach (var point in FindObjectsByType<TeamSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (point.Team == Team.Red)
                redPoints.Add(point);
            else if (point.Team == Team.Blue)
                bluePoints.Add(point);
        }

        // Ordre stable pour une attribution déterministe
        redPoints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        bluePoints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (redPoints.Count == 0 || bluePoints.Count == 0)
        {
            Debug.LogWarning("[GameState] Pas de TeamSpawnPoint dans la scène — les joueurs restent où ils sont.");
            return;
        }

        Vector3 redCenter = AveragePosition(redPoints);
        Vector3 blueCenter = AveragePosition(bluePoints);

        int redIndex = 0;
        int blueIndex = 0;

        foreach (var profile in FindObjectsByType<PlayerProfile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (profile == null || profile.Object == null || profile.Object.IsValid == false)
                continue;

            Team team = profile.Team;
            if (team == Team.None)
                continue;

            TeamSpawnPoint point;
            Vector3 faceTarget;
            if (team == Team.Red)
            {
                point = redPoints[redIndex % redPoints.Count];
                redIndex++;
                faceTarget = blueCenter;
            }
            else
            {
                point = bluePoints[blueIndex % bluePoints.Count];
                blueIndex++;
                faceTarget = redCenter;
            }

            Vector3 direction = faceTarget - point.transform.position;
            direction.y = 0f;
            float yaw = direction.sqrMagnitude > 0.01f
                ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg
                : 0f;

            profile.TeleportTo(point.transform.position + Vector3.up * 0.5f, yaw);
        }
    }

    private static Vector3 AveragePosition(List<TeamSpawnPoint> points)
    {
        var sum = Vector3.zero;
        foreach (var point in points)
            sum += point.transform.position;
        return sum / points.Count;
    }

    // Déplace les rectangles de zone sur le point de l'étape courante.
    // Exécuté chez tous les clients : le chrono est réseau, tout le monde
    // déplace les mêmes objets au même moment.
    private void MoveZonesToCurrentStep()
    {
        int step = IsStarted ? CurrentStep : 0;
        if (step == _appliedStep)
            return;

        RefreshZoneMarkers();
        if (_stepPoints == null || _stepPoints.Length == 0)
            _stepPoints = FindObjectsByType<ZoneStepPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (_stepPoints.Length == 0)
            return;

        bool redMoved = TryMoveZone(_redZoneMarker, Team.Red, step);
        bool blueMoved = TryMoveZone(_blueZoneMarker, Team.Blue, step);

        if (redMoved && blueMoved)
        {
            // Son de déplacement (chaque client exécute ce code au même moment)
            bool isStepChange = _appliedStep >= 0 && step != _appliedStep;
            if (isStepChange && _zoneMoveClip != null && Camera.main != null)
                AudioSource.PlayClipAtPoint(_zoneMoveClip, Camera.main.transform.position, 0.8f);

            _appliedStep = step;
        }
    }

    private bool TryMoveZone(CollectionZoneMarker marker, Team team, int step)
    {
        if (marker == null)
            return false;

        foreach (var point in _stepPoints)
        {
            if (point == null || point.Team != team || point.Step != step)
                continue;

            marker.transform.position = point.transform.position;
            return true;
        }

        // Pas de point défini pour cette étape : la zone reste où elle est
        return true;
    }

    // Despawne tous les objets Grabbable présents dans la zone d'une équipe à
    // la position d'une étape donnée (l'ancienne, juste avant le saut de zone)
    private void ConsumeObjectsInZone(Team team, int step)
    {
        RefreshZoneMarkers();
        var marker = team == Team.Red ? _redZoneMarker : _blueZoneMarker;

        // Position de la zone à l'étape consommée (le marqueur n'a pas encore sauté
        // côté serveur, mais on la recalcule depuis le point d'étape par sûreté)
        Vector3 center = marker != null ? marker.Center : (team == Team.Red ? _redZoneCenter : _blueZoneCenter);
        Vector3 halfExtents = marker != null ? marker.HalfExtents : _zoneHalfExtents;

        if (_stepPoints != null)
        {
            foreach (var point in _stepPoints)
            {
                if (point != null && point.Team == team && point.Step == step)
                {
                    center = point.transform.position + Vector3.up * halfExtents.y;
                    break;
                }
            }
        }

        var consumed = new HashSet<GameObject>();
        var hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (root.CompareTag("Grabbable") == false || consumed.Add(root) == false)
                continue;

            var networkObject = root.GetComponent<NetworkObject>();
            if (networkObject != null)
                Runner.Despawn(networkObject);
            else
                Destroy(root);
        }

        if (consumed.Count > 0)
            Debug.Log($"[GameState] Zone {team} étape {step + 1} : {consumed.Count} objet(s) consommé(s) avec la zone.");
    }

    /// <summary>Centre actuel de la zone de collecte d'une équipe (sort fortuna).</summary>
    public Vector3 GetZoneCenter(Team team)
    {
        RefreshZoneMarkers();
        var marker = team == Team.Red ? _redZoneMarker : _blueZoneMarker;
        if (marker != null)
            return marker.transform.position;
        return team == Team.Red ? _redZoneCenter : _blueZoneCenter;
    }

    private void RefreshZoneMarkers()
    {
        if (_redZoneMarker != null && _blueZoneMarker != null)
            return;

        foreach (var marker in FindObjectsByType<CollectionZoneMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (marker.Team == Team.Red)
                _redZoneMarker = marker;
            else if (marker.Team == Team.Blue)
                _blueZoneMarker = marker;
        }
    }

    // Compte les objets requis présents dans la zone de l'équipe :
    // - seuls les types listés dans le ZoneStepPoint (équipe + étape courante) comptent
    // - chaque type est plafonné à son NbObj (les objets en trop n'apportent rien)
    // - chaque objet compte pour sa Value
    private MyDataTuple CountCollectiblesInZone(CollectionZoneMarker marker, Vector3 fallbackCenter, Team team, int step)
    {
        Vector3 center = marker != null ? marker.Center : fallbackCenter;
        Vector3 halfExtents = marker != null ? marker.HalfExtents : _zoneHalfExtents;

        // Le point d'étape de CETTE équipe à CETTE étape définit les types requis
        if (_stepPoints == null || _stepPoints.Length == 0)
            _stepPoints = FindObjectsByType<ZoneStepPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        ZoneStepPoint stepPoint = null;
        foreach (var point in _stepPoints)
        {
            if (point != null && point.Team == team && point.Step == step)
            {
                stepPoint = point;
                break;
            }
        }

        if (stepPoint == null)
            return new MyDataTuple(0, 0);

        var countedObjects = new HashSet<GameObject>();
        var countPerType = new Dictionary<ValueCollectible.TypeObj, int>();
        int total = 0;

        var hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;

            // Dédoublonne les objets touchés par plusieurs de leurs colliders
            if (root.CompareTag("Grabbable") == false || countedObjects.Add(root) == false)
                continue;

            var collectible = root.GetComponent<ValueCollectible>();
            if (collectible == null)
                continue;

            int maxOfType = stepPoint.SearchTypeObj(collectible.Type);
            if (maxOfType <= 0)
                continue;

            countPerType.TryGetValue(collectible.Type, out int alreadyCounted);
            if (alreadyCounted >= maxOfType)
                continue;

            countPerType[collectible.Type] = alreadyCounted + 1;
            total += Mathf.Max(1, collectible.Value);
        }

        return new MyDataTuple(countedObjects.Count, total);
    }
}