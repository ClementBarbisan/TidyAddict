using Fusion;
using UnityEngine;

/// <summary>
/// Effets de sort actifs sur un joueur : ralentissement (glace), buff de
/// vitesse (aurora), buff de force (maximus), invisibilité (anima).
/// Les timers sont posés par le serveur ; les multiplicateurs sont lus par
/// Player (vitesse) et PlayerGrabbing (force), l'invisibilité est appliquée
/// visuellement chez les autres clients dans Render.
/// </summary>
public class PlayerSpellEffects : NetworkBehaviour
{
    [SerializeField] private float _slowMultiplier = 0.45f;
    [SerializeField] private float _speedBuffMultiplier = 1.6f;
    [SerializeField] private float _forceBuffMultiplier = 2.5f;

    [Networked] private TickTimer SlowTimer { get; set; }
    [Networked] private TickTimer SpeedBuffTimer { get; set; }
    [Networked] private TickTimer ForceBuffTimer { get; set; }
    [Networked] private TickTimer InvisibilityTimer { get; set; }
    [Networked] private TickTimer KnockbackTimer { get; set; }
    [Networked] private Vector3 KnockbackVelocity { get; set; }

    private Renderer[] _renderers;

    public bool IsSlowed => Object != null && Object.IsValid && SlowTimer.ExpiredOrNotRunning(Runner) == false;
    public bool HasSpeedBuff => Object != null && Object.IsValid && SpeedBuffTimer.ExpiredOrNotRunning(Runner) == false;
    public bool HasForceBuff => Object != null && Object.IsValid && ForceBuffTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsInvisible => Object != null && Object.IsValid && InvisibilityTimer.ExpiredOrNotRunning(Runner) == false;

    public float SpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            if (IsSlowed)
                multiplier *= _slowMultiplier;
            if (HasSpeedBuff)
                multiplier *= _speedBuffMultiplier;
            return multiplier;
        }
    }

    public float ForceMultiplier => HasForceBuff ? _forceBuffMultiplier : 1f;

    /// <summary>Poussée en cours (explosion de boule de feu), ajoutée au mouvement du joueur.</summary>
    public Vector3 CurrentKnockback
    {
        get
        {
            if (Object == null || Object.IsValid == false || KnockbackTimer.ExpiredOrNotRunning(Runner))
                return Vector3.zero;
            return KnockbackVelocity;
        }
    }

    // Appelés côté serveur (state authority)

    public void ApplySlow(float seconds)
    {
        SlowTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public void ApplySpeedBuff(float seconds)
    {
        SpeedBuffTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public void ApplyForceBuff(float seconds)
    {
        ForceBuffTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public void ApplyInvisibility(float seconds)
    {
        InvisibilityTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public void ApplyKnockback(Vector3 velocity, float seconds = 0.35f)
    {
        KnockbackVelocity = velocity;
        KnockbackTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public override void Render()
    {
        // Invisibilité : on cache le corps du joueur chez les AUTRES clients.
        // Le joueur local est en vue première personne (son mesh est déjà géré
        // par Player.LateUpdate), on n'y touche pas pour éviter les conflits.
        if (HasInputAuthority)
            return;

        if (_renderers == null)
            _renderers = GetComponentsInChildren<Renderer>(true);

        bool visible = IsInvisible == false;
        foreach (var childRenderer in _renderers)
        {
            if (childRenderer != null && childRenderer.enabled != visible)
                childRenderer.enabled = visible;
        }
    }
}
