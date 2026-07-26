using Fusion;
using UnityEngine;

/// <summary>
/// Mur de pierre (sort petra) : surgit du sol devant le lanceur, bloque les
/// joueurs (et les projectiles) pendant 30 s, puis s'enfonce et disparaît.
/// Le mouvement est simulé par le serveur et répliqué via NetworkTransform.
/// </summary>
public class SpellWall : NetworkBehaviour
{
    [SerializeField] private float _lifeSeconds = 30f;
    [SerializeField] private float _riseSeconds = 0.6f;
    [SerializeField] private float _height = 3f;

    [Networked] private TickTimer Life { get; set; }
    [Networked] private Vector3 BasePosition { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Life = TickTimer.CreateFromSeconds(Runner, _lifeSeconds);
            BasePosition = transform.position;
            // Départ enterré : le mur va sortir du sol
            transform.position = BasePosition + Vector3.down * _height;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
            return;

        if (Life.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        float remaining = Life.RemainingTime(Runner) ?? 0f;
        float elapsed = _lifeSeconds - remaining;

        // Montée au début, descente sur la fin, stable entre les deux
        float offset;
        if (elapsed < _riseSeconds)
            offset = Mathf.Lerp(-_height, 0f, elapsed / _riseSeconds);
        else if (remaining < _riseSeconds)
            offset = Mathf.Lerp(-_height, 0f, remaining / _riseSeconds);
        else
            offset = 0f;

        transform.position = BasePosition + Vector3.up * offset;
    }
}
