using Fusion;
using UnityEngine;

/// <summary>
/// Boule de sort : vole tout droit, pousse le premier objet physique touché,
/// puis disparaît (ou expire au bout de quelques secondes).
/// </summary>
public class SpellBall : NetworkBehaviour
{
    [SerializeField] private float _speed = 18f;
    [SerializeField] private float _lifeSeconds = 5f;
    [SerializeField] private float _hitImpulse = 10f;

    [Networked] private TickTimer Life { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Life = TickTimer.CreateFromSeconds(Runner, _lifeSeconds);
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

        float step = _speed * Runner.DeltaTime;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step + 0.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(transform.forward * _hitImpulse, hit.point, ForceMode.Impulse);
            }

            Runner.Despawn(Object);
            return;
        }

        transform.position += transform.forward * step;
    }
}
