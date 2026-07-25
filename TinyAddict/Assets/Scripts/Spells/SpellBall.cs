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
    [SerializeField] private float _hitImpulse = 16f;
    [SerializeField] private float _hitUpImpulse = 6f;

    [Networked] private TickTimer Life { get; set; }

    // Index du mot/sort dans SpellWords : détermine la couleur (et plus tard l'effet)
    [Networked] public int SpellIndex { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Life = TickTimer.CreateFromSeconds(Runner, _lifeSeconds);
        }

        ApplySpellVisual();
    }

    private void ApplySpellVisual()
    {
        Color color = SpellWords.ColorOf(SpellIndex);

        var renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 3f);
            renderer.SetPropertyBlock(block);
        }

        var light = GetComponentInChildren<Light>();
        if (light != null)
            light.color = color;
    }

    [SerializeField] private float _explosionRadius = 4f;
    [SerializeField] private float _explosionForce = 12f;

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
            Explode(hit.point);
            Runner.Despawn(Object);
            return;
        }

        transform.position += transform.forward * step;
    }

    // Projette tout ce qui se trouve dans le rayon : objets physiques et joueurs
    private void Explode(Vector3 center)
    {
        var hits = Physics.OverlapSphere(center, _explosionRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hitCollider in hits)
        {
            var rigidbody = hitCollider.attachedRigidbody;
            if (rigidbody != null)
            {
                rigidbody.AddExplosionForce(_explosionForce, center, _explosionRadius, 0.5f, ForceMode.Impulse);
                continue;
            }

            var effects = hitCollider.GetComponentInParent<PlayerSpellEffects>();
            if (effects != null)
            {
                // Éjection : poussée horizontale qui s'atténue avec la distance + décollage vertical
                Vector3 away = effects.transform.position - center;
                away.y = 0f;
                float distanceFactor = 1f - Mathf.Clamp01(away.magnitude / _explosionRadius) * 0.5f;
                Vector3 horizontal = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;

                Vector3 knockback = horizontal * (_hitImpulse * distanceFactor);
                knockback.y = _hitUpImpulse;
                effects.ApplyKnockback(knockback);
            }
        }
    }
}
