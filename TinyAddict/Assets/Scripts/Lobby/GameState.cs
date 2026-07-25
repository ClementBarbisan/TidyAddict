using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// État global de la partie, spawné par le serveur au démarrage de la session.
/// - Lobby : la partie est lancée par l'hôte quand assez de joueurs sont là.
/// - Match : chrono de 5 min ; chaque équipe doit ramener les objets
///   « Grabbable » dans SA zone de collecte. Le serveur compte les objets
///   dans chaque zone ; à la fin du chrono, l'équipe avec le plus haut %
///   de collecte gagne.
/// </summary>
public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    [SerializeField] private int _requiredPlayers = 4;
    [SerializeField] private float _matchSeconds = 300f;

    // Zones de collecte (boîtes centrées sur ces points) — les visuels de la
    // scène sont créés par le setup éditeur aux mêmes positions
    [SerializeField] private Vector3 _redZoneCenter = new Vector3(-12f, 1f, 0f);
    [SerializeField] private Vector3 _blueZoneCenter = new Vector3(12f, 1f, 0f);
    [SerializeField] private Vector3 _zoneHalfExtents = new Vector3(4f, 2f, 4f);

    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool GameEnded { get; set; }
    [Networked] public int WinnerTeam { get; set; }        // 0 = égalité, 1 = rouge, 2 = bleu
    [Networked] public TickTimer MatchTimer { get; set; }
    [Networked] public int TotalCollectibles { get; set; }
    [Networked] public int RedCollected { get; set; }
    [Networked] public int BlueCollected { get; set; }

    public int RequiredPlayers => _requiredPlayers;
    public int MaxPlayersPerTeam => Mathf.Max(1, _requiredPlayers / 2);
    public Vector3 RedZoneCenter => _redZoneCenter;
    public Vector3 BlueZoneCenter => _blueZoneCenter;
    public Vector3 ZoneHalfExtents => _zoneHalfExtents;

    public bool IsStarted => Object != null && Object.IsValid && GameStarted;
    public bool IsEnded => Object != null && Object.IsValid && GameEnded;
    public bool CanStart => ConnectedPlayers >= _requiredPlayers;

    public float RedPercent => TotalCollectibles > 0 ? RedCollected / (float)TotalCollectibles : 0f;
    public float BluePercent => TotalCollectibles > 0 ? BlueCollected / (float)TotalCollectibles : 0f;

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
        TotalCollectibles = GameObject.FindGameObjectsWithTag("Grabbable").Length;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false || GameStarted == false || GameEnded)
            return;

        // Comptage des zones ~4 fois par seconde, inutile de le faire à chaque tick
        if (Runner.Tick % 16 == 0)
        {
            RedCollected = CountCollectiblesInZone(_redZoneCenter);
            BlueCollected = CountCollectiblesInZone(_blueZoneCenter);
        }

        if (MatchTimer.Expired(Runner))
        {
            GameEnded = true;

            // Dernier comptage pour le verdict
            RedCollected = CountCollectiblesInZone(_redZoneCenter);
            BlueCollected = CountCollectiblesInZone(_blueZoneCenter);

            if (RedCollected > BlueCollected)
                WinnerTeam = (int)Team.Red;
            else if (BlueCollected > RedCollected)
                WinnerTeam = (int)Team.Blue;
            else
                WinnerTeam = 0;
        }
    }

    private int CountCollectiblesInZone(Vector3 center)
    {
        var counted = new HashSet<GameObject>();
        var hits = Physics.OverlapBox(center, _zoneHalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (root.CompareTag("Grabbable"))
                counted.Add(root);
        }

        return counted.Count;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.5f);
        Gizmos.DrawWireCube(_redZoneCenter, _zoneHalfExtents * 2f);
        Gizmos.color = new Color(0.3f, 0.55f, 1f, 0.5f);
        Gizmos.DrawWireCube(_blueZoneCenter, _zoneHalfExtents * 2f);
    }
}
