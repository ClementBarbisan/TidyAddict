using Fusion;
using UnityEngine;

/// <summary>
/// Trou noir (sort pluto) : aspire joueurs et objets physiques vers son centre
/// pendant quelques secondes, puis disparaît. Le lanceur n'est pas aspiré.
/// </summary>
public class BlackHoleZone : NetworkBehaviour
{
    [SerializeField] private float _radius = 6f;
    [SerializeField] private float _lifeSeconds = 4f;
    [SerializeField] private float _playerPullSpeed = 7f;
    [SerializeField] private float _objectPullForce = 18f;

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

        var counted = new System.Collections.Generic.HashSet<Rigidbody>();
        var hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            // Joueurs : aspiration continue via le knockback réseau (sauf le lanceur)
            var effects = hit.GetComponentInParent<PlayerSpellEffects>();
            if (effects != null)
            {
                if (effects.Object == null || effects.Object.IsValid == false)
                    continue;
                if (effects.Object.InputAuthority == Caster)
                    continue;

                Vector3 toCenter = transform.position - effects.transform.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.25f)
                    effects.ApplyKnockback(toCenter.normalized * _playerPullSpeed, 0.2f);
                continue;
            }

            // Objets physiques : force d'attraction
            var rigidbody = hit.attachedRigidbody;
            if (rigidbody != null && rigidbody.isKinematic == false && counted.Add(rigidbody))
            {
                Vector3 toCenter = (transform.position - rigidbody.worldCenterOfMass).normalized;
                rigidbody.AddForce(toCenter * _objectPullForce, ForceMode.Acceleration);
            }
        }
    }

    public override void Render()
    {
        // Rotation hypnotique du visuel
        var visual = transform.Find("Visual");
        if (visual != null)
            visual.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);
    }
}
