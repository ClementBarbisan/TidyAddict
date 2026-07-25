using Fusion;
using UnityEngine;

/// <summary>
/// État global de la partie, spawné par le serveur au démarrage de la session.
/// La partie est lancée par l'hôte quand assez de joueurs sont connectés
/// (ou en force avec le bouton debug). Tant qu'elle n'est pas lancée, les
/// parchemins ne spawnnent pas.
/// </summary>
public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    [SerializeField] private int _requiredPlayers = 4;

    [Networked] public NetworkBool GameStarted { get; set; }

    public int RequiredPlayers => _requiredPlayers;

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

    public bool IsStarted => Object != null && Object.IsValid && GameStarted;
    public bool CanStart => ConnectedPlayers >= _requiredPlayers;

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
        if (Object.HasStateAuthority)
            GameStarted = true;
    }
}
