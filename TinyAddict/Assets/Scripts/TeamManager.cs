using Fusion;
using UnityEngine;
using System.Collections.Generic;

public enum Team
{
    None,
    Red,
    Blue
}

public class TeamManager : NetworkBehaviour
{
    public static TeamManager Instance { get; private set; }

    // Dictionnaire synchronisé pour stocker l'équipe de chaque joueur (Key: PlayerRef, Value: Team)
    [Networked, Capacity(32)] private NetworkDictionary<PlayerRef, Team> PlayerTeams => default;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Change l'équipe d'un joueur (exécuté sur le serveur/hôte)
    /// </summary>
    public void SetPlayerTeam(PlayerRef player, Team newTeam)
    {
        if (!HasStateAuthority) return;

        if (PlayerTeams.ContainsKey(player))
        {
            PlayerTeams.Set(player, newTeam);
        }
        else
        {
            PlayerTeams.Add(player, newTeam);
        }

        Debug.Log($"[TeamManager] Le joueur {player.PlayerId} a rejoint l'équipe : {newTeam}");
    }

    /// <summary>
    /// Obtenir l'équipe actuelle d'un joueur
    /// </summary>
    public Team GetPlayerTeam(PlayerRef player)
    {
        if (PlayerTeams.ContainsKey(player))
        {
            return PlayerTeams.Get(player);
        }
        return Team.None;
    }
}