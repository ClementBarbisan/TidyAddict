using Fusion;
using UnityEngine;

/// <summary>
/// Zone de glace (sort polaris) : grand disque gelé au sol.
/// - Les ENNEMIS dedans sont ralentis et glissent (inertie) — le lanceur
///   n'est affecté par rien.
/// - Les objets physiques glissent : leur élan est entretenu tant qu'ils
///   sont sur la glace.
/// </summary>
public class IceZone : NetworkBehaviour
{
    [SerializeField] private float _radius = 7f;
    [SerializeField] private float _lifeSeconds = 6f;
    [SerializeField] private float _slowSeconds = 1f;
    [SerializeField] private float _objectSlipForce = 6f;
    [SerializeField] private float _objectMaxSlideSpeed = 8f;

    [Networked] private TickTimer Life { get; set; }
    [Networked] public PlayerRef Caster { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Life = TickTimer.CreateFromSeconds(Runner, _lifeSeconds);
        }

        // Le visuel colle au rayon de la zone, chez tout le monde
        var visual = transform.Find("Visual");
        if (visual != null)
            visual.localScale = new Vector3(_radius * 2f, visual.localScale.y, _radius * 2f);
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

        var counted = new System.Collections.Generic.HashSet<Rigidbody>();
        var hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            var effects = hit.GetComponentInParent<PlayerSpellEffects>();
            if (effects != null)
            {
                if (effects.Object == null || effects.Object.IsValid == false)
                    continue;

                // Le lanceur n'est affecté ni par le ralentissement ni par la glisse
                if (effects.Object.InputAuthority == Caster)
                    continue;

                // Ralentissement glissant : réappliqué chaque tick tant qu'on est
                // dans la zone, il expire de lui-même peu après en être sorti
                effects.ApplySlow(_slowSeconds);
                effects.ApplyOnIce();
                continue;
            }

            // Objets : la glace entretient leur élan (glisse)
            var rigidbody = hit.attachedRigidbody;
            if (rigidbody != null && rigidbody.isKinematic == false && counted.Add(rigidbody))
            {
                Vector3 velocity = rigidbody.linearVelocity;
                velocity.y = 0f;
                float speed = velocity.magnitude;
                if (speed > 0.3f && speed < _objectMaxSlideSpeed)
                    rigidbody.AddForce(velocity.normalized * _objectSlipForce, ForceMode.Acceleration);
            }
        }
    }
}
