using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Fait vivre les parchemins sur les ScrollSpawnPoint placés dans la scène :
/// un parchemin par point au lancement de la partie, et quand l'un est
/// consommé par un sort, un nouveau réapparaît sur son point après un délai
/// (avec un mot/sort aléatoire). Côté serveur uniquement.
/// À placer sur le GameObject du NetworkRunner.
/// </summary>
public class SpellScrollSpawner : SimulationBehaviour
{
    [SerializeField] private NetworkObject _scrollPrefab;
    [SerializeField] private float _respawnDelay = 3f;

    private ScrollSpawnPoint[] _points;
    private readonly Dictionary<ScrollSpawnPoint, NetworkObject> _scrollByPoint = new Dictionary<ScrollSpawnPoint, NetworkObject>(16);
    private readonly Dictionary<ScrollSpawnPoint, TickTimer> _respawnByPoint = new Dictionary<ScrollSpawnPoint, TickTimer>(16);
    private bool _warnedNoPoints;

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer == false || _scrollPrefab == null)
            return;

        // Pas de parchemins avant le lancement depuis le lobby, ni après la fin
        // du match : on purge ceux qui restent (retour au lobby = table rase)
        if (GameState.Instance == null || GameState.Instance.IsStarted == false || GameState.Instance.IsEnded)
        {
            CleanupScrolls();
            return;
        }

        if (_points == null || _points.Length == 0)
        {
            _points = FindObjectsByType<ScrollSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (_points.Length == 0)
            {
                if (_warnedNoPoints == false)
                {
                    _warnedNoPoints = true;
                    Debug.LogWarning("[SpellScrollSpawner] Aucun ScrollSpawnPoint dans la scène — aucun parchemin ne spawnera.");
                }
                return;
            }
        }

        foreach (var point in _points)
        {
            if (point == null)
                continue;

            // Le parchemin de ce point est toujours vivant (au sol ou en main)
            if (_scrollByPoint.TryGetValue(point, out var scroll) && scroll != null)
                continue;

            bool neverSpawned = _scrollByPoint.ContainsKey(point) == false;
            if (neverSpawned)
            {
                SpawnAt(point);
                continue;
            }

            // Parchemin consommé : on arme le délai, puis on respawn sur le point
            if (_respawnByPoint.TryGetValue(point, out var timer) == false || timer.IsRunning == false)
            {
                _respawnByPoint[point] = TickTimer.CreateFromSeconds(Runner, _respawnDelay);
                continue;
            }

            if (timer.Expired(Runner))
            {
                _respawnByPoint.Remove(point);
                SpawnAt(point);
            }
        }
    }

    private void SpawnAt(ScrollSpawnPoint point)
    {
        var position = point.transform.position + Vector3.up * 0.05f;
        _scrollByPoint[point] = Runner.Spawn(_scrollPrefab, position, Quaternion.identity);
    }

    private void CleanupScrolls()
    {
        if (_scrollByPoint.Count == 0 && _respawnByPoint.Count == 0)
            return;

        foreach (var scroll in _scrollByPoint.Values)
        {
            if (scroll != null)
                Runner.Despawn(scroll);
        }

        _scrollByPoint.Clear();
        _respawnByPoint.Clear();
    }
}
