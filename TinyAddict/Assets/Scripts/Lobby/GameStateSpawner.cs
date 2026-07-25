using Fusion;
using UnityEngine;

/// <summary>
/// Spawne l'objet réseau GameState une seule fois au démarrage de la session,
/// côté serveur. À placer sur le GameObject du NetworkRunner.
/// </summary>
public class GameStateSpawner : SimulationBehaviour
{
    [SerializeField] private NetworkObject _gameStatePrefab;

    private bool _spawned;

    public override void FixedUpdateNetwork()
    {
        if (_spawned || Runner.IsServer == false || _gameStatePrefab == null)
            return;

        _spawned = true;
        Runner.Spawn(_gameStatePrefab, Vector3.zero, Quaternion.identity);
    }
}
