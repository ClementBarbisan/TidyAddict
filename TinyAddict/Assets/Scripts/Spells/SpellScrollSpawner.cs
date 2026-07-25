using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Maintient un nombre constant de parchemins en jeu, côté serveur uniquement.
/// Le stock initial apparaît au démarrage de la session à des positions
/// aléatoires dans la zone ; quand un parchemin est consommé par un sort,
/// un nouveau réapparaît ailleurs après un court délai.
/// À placer sur le GameObject du NetworkRunner.
/// </summary>
public class SpellScrollSpawner : SimulationBehaviour
{
    [SerializeField] private NetworkObject _scrollPrefab;
    [SerializeField] private int _scrollCount = 5;
    [SerializeField] private Vector3 _areaCenter = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private Vector2 _areaSize = new Vector2(14f, 14f);
    [SerializeField] private float _respawnDelay = 3f;

    private readonly List<NetworkObject> _scrolls = new List<NetworkObject>(16);
    private TickTimer _respawnTimer;
    private bool _initialSpawnDone;

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer == false || _scrollPrefab == null)
            return;

        // Pas de parchemins tant que la partie n'est pas lancée depuis le lobby
        if (GameState.Instance == null || GameState.Instance.IsStarted == false)
            return;

        // Les parchemins consommés (despawnés) deviennent null
        _scrolls.RemoveAll(scroll => scroll == null);

        if (_initialSpawnDone == false)
        {
            _initialSpawnDone = true;
            while (_scrolls.Count < _scrollCount)
                SpawnScroll();
            return;
        }

        if (_scrolls.Count >= _scrollCount)
        {
            _respawnTimer = default;
            return;
        }

        // Un parchemin manque : on arme le délai, puis on respawn à expiration
        if (_respawnTimer.IsRunning == false)
        {
            _respawnTimer = TickTimer.CreateFromSeconds(Runner, _respawnDelay);
            return;
        }

        if (_respawnTimer.Expired(Runner))
        {
            SpawnScroll();
            _respawnTimer = default;
        }
    }

    private void SpawnScroll()
    {
        var position = _areaCenter + new Vector3(
            Random.Range(-_areaSize.x * 0.5f, _areaSize.x * 0.5f),
            0f,
            Random.Range(-_areaSize.y * 0.5f, _areaSize.y * 0.5f));

        _scrolls.Add(Runner.Spawn(_scrollPrefab, position, Quaternion.identity));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.5f);
        Gizmos.DrawWireCube(_areaCenter, new Vector3(_areaSize.x, 0.1f, _areaSize.y));
    }
}
