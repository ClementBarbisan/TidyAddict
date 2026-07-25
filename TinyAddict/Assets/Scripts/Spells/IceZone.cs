using Fusion;
using UnityEngine;

/// <summary>
/// Zone de glace (sort polaris) : disque bleu au sol qui ralentit tous les
/// joueurs à l'intérieur SAUF le lanceur, tant que la zone est active.
/// </summary>
public class IceZone : NetworkBehaviour
{
    [SerializeField] private float _radius = 4f;
    [SerializeField] private float _lifeSeconds = 6f;
    [SerializeField] private float _slowSeconds = 1f;

    [Networked] private TickTimer Life { get; set; }
    [Networked] public PlayerRef Caster { get; set; }

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

        // Ralentissement glissant : réappliqué chaque tick tant qu'on est dans
        // la zone, il expire de lui-même peu après en être sorti
        var hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var effects = hit.GetComponentInParent<PlayerSpellEffects>();
            if (effects == null || effects.Object == null || effects.Object.IsValid == false)
                continue;

            if (effects.Object.InputAuthority == Caster)
                continue;

            effects.ApplySlow(_slowSeconds);
        }
    }
}
