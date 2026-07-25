using Fusion;
using UnityEngine;

/// <summary>
/// Spawne les parchemins via Runner.Spawn aux points de spawn définis, une seule
/// fois au démarrage de la session, côté serveur uniquement. Plus fiable que des
/// parchemins posés dans la scène (pas de dépendance au baking des objets de scène).
/// À placer sur le GameObject du NetworkRunner.
/// </summary>
public class SpellScrollSpawner : SimulationBehaviour
{
    [SerializeField] private NetworkObject _scrollPrefab;
    [SerializeField] private Vector3[] _spawnPoints =
    {
        new Vector3(3f, 0.05f, 4f),
        new Vector3(-4f, 0.05f, 3f),
        new Vector3(6f, 0.05f, -2f),
        new Vector3(-2f, 0.05f, -5f),
    };

    private bool _spawned;

    public override void FixedUpdateNetwork()
    {
        if (_spawned || Runner.IsServer == false || _scrollPrefab == null)
            return;

        _spawned = true;

        foreach (var point in _spawnPoints)
        {
            Runner.Spawn(_scrollPrefab, point, Quaternion.identity);
        }
    }
}
