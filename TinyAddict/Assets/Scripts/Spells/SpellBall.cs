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
    [SerializeField] private float _hitImpulse = 18f;
    [SerializeField] private float _hitUpImpulse = 8f;

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
    [SerializeField] private AudioClip _impactClip;

    // Despawned s'exécute chez TOUS les clients : le son d'impact joue partout
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_impactClip != null)
            AudioSource.PlayClipAtPoint(_impactClip, transform.position, 0.9f);
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
            Explode(hit.point);
            Runner.Despawn(Object);
            return;
        }

        transform.position += transform.forward * step;
    }

    // Projette tout ce qui se trouve dans le rayon : objets physiques et joueurs
    private void Explode(Vector3 center)
    {
        var hitPlayers = new System.Collections.Generic.HashSet<PlayerSpellEffects>();
        var hitBodies = new System.Collections.Generic.HashSet<Rigidbody>();

        var hits = Physics.OverlapSphere(center, _explosionRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hitCollider in hits)
        {
            // Les joueurs d'abord : leur capsule KCC porte un Rigidbody kinematic,
            // insensible aux forces physiques — l'éjection passe par le knockback réseau
            var effects = hitCollider.GetComponentInParent<PlayerSpellEffects>();
            if (effects != null)
            {
                if (hitPlayers.Add(effects))
                {
                    // Poussée horizontale atténuée avec la distance + décollage vertical
                    Vector3 away = effects.transform.position - center;
                    away.y = 0f;
                    float distanceFactor = 1f - Mathf.Clamp01(away.magnitude / _explosionRadius) * 0.5f;
                    Vector3 horizontal = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;

                    Vector3 knockback = horizontal * (_hitImpulse * distanceFactor);
                    knockback.y = _hitUpImpulse;
                    effects.ApplyKnockback(knockback);
                }
                continue;
            }

            var rigidbody = hitCollider.attachedRigidbody;
            if (rigidbody != null && rigidbody.isKinematic == false && hitBodies.Add(rigidbody))
            {
                rigidbody.AddExplosionForce(_explosionForce, center, _explosionRadius, 0.5f, ForceMode.Impulse);
            }
        }
    }
}
