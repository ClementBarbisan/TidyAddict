using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private GameObject _prefabPlayer;
    
    void IPlayerJoined.PlayerJoined(PlayerRef player)
    {
        Debug.Log("Player Connected");
        if (player == Runner.LocalPlayer)
        {
            Runner.Spawn(_prefabPlayer, new Vector3(0, 1, 0), Quaternion.identity);
        }
    }
}
