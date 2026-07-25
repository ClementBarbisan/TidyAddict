using System.Collections.Generic;
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
    public static GameState Instance { get; private set; }

    [SerializeField] private int _requiredPlayers = 4;
    [SerializeField] private float _matchSeconds = 300f;
    [SerializeField] private int _zoneSteps = 5;

    [Tooltip("Charge apportée par UN objet resté UNE minute dans la zone (0.05 = 5 % → 5 objets × 1 min = 25 %)")]
    [SerializeField] private float _chargePerObjectPerMinute = 0.05f;

    // Position/taille de secours si aucun CollectionZoneMarker n'est trouvé
    [SerializeField] private Vector3 _redZoneCenter = new Vector3(-12f, 1f, 0f);
    [SerializeField] private Vector3 _blueZoneCenter = new Vector3(12f, 1f, 0f);
    [SerializeField] private Vector3 _zoneHalfExtents = new Vector3(4f, 2f, 4f);

    private CollectionZoneMarker _redZoneMarker;
    private CollectionZoneMarker _blueZoneMarker;
    private ZoneStepPoint[] _stepPoints;
    private int _appliedStep = -1;

    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool GameEnded { get; set; }
    [Networked] public int WinnerTeam { get; set; }        // 0 = égalité, 1 = rouge, 2 = bleu
    [Networked] public TickTimer MatchTimer { get; set; }
    [Networked] public float RedCharge { get; set; }
    [Networked] public float BlueCharge { get; set; }
    [Networked] public int RedCollected { get; set; }
    [Networked] public int BlueCollected { get; set; }

    public int RequiredPlayers => _requiredPlayers;
    public int MaxPlayersPerTeam => Mathf.Max(1, _requiredPlayers / 2);

    public bool IsStarted => Object != null && Object.IsValid && GameStarted;
    public bool IsEnded => Object != null && Object.IsValid && GameEnded;
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

    /// <summary>Appelé côté hôte uniquement (state authority).</summary>
    public void StartGame()
    {
        if (Object.HasStateAuthority == false || GameStarted)
            return;

        GameStarted = true;
        MatchTimer = TickTimer.CreateFromSeconds(Runner, _matchSeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false || GameStarted == false || GameEnded)
            return;

        // Comptage des zones ~4 fois par seconde, inutile de le faire à chaque tick
        if (Runner.Tick % 16 == 0)
        {
            RefreshZoneMarkers();
            RedCollected = CountCollectiblesInZone(_redZoneMarker, _redZoneCenter);
            BlueCollected = CountCollectiblesInZone(_blueZoneMarker, _blueZoneCenter);
        }

        // Les jauges chargent en continu : chaque objet présent dans la zone
        // apporte _chargePerObjectPerMinute par minute
        float chargePerObjectPerSecond = _chargePerObjectPerMinute / 60f;
        RedCharge = Mathf.Clamp01(RedCharge + RedCollected * chargePerObjectPerSecond * Runner.DeltaTime);
        BlueCharge = Mathf.Clamp01(BlueCharge + BlueCollected * chargePerObjectPerSecond * Runner.DeltaTime);

        // Victoire immédiate à 100 %
        bool redWinsNow = RedCharge >= 1f;
        bool blueWinsNow = BlueCharge >= 1f;

        if (redWinsNow || blueWinsNow || MatchTimer.Expired(Runner))
        {
            GameEnded = true;

            if (RedCharge > BlueCharge)
                WinnerTeam = (int)Team.Red;
            else if (BlueCharge > RedCharge)
                WinnerTeam = (int)Team.Blue;
            else
                WinnerTeam = 0;
        }
    }

    public override void Render()
    {
        MoveZonesToCurrentStep();
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
            _appliedStep = step;
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

    private int CountCollectiblesInZone(CollectionZoneMarker marker, Vector3 fallbackCenter)
    {
        Vector3 center = marker != null ? marker.Center : fallbackCenter;
        Vector3 halfExtents = marker != null ? marker.HalfExtents : _zoneHalfExtents;

        var counted = new HashSet<GameObject>();
        var hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (root.CompareTag("Grabbable"))
                counted.Add(root);
        }

        return counted.Count;
    }
}
