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
    [SerializeField] private float _shrinkForceMultiplier = 0.3f;
    [SerializeField] private float _shrinkScale = 0.4f;
    [SerializeField] private float _shrinkVoicePitch = 1.6f;
    [SerializeField] private float _growScale = 1.5f;
    [SerializeField] private float _growVoicePitch = 0.7f;
    [SerializeField] private float _chargeBumpImpulse = 12f;
    [SerializeField] private float _chargeBumpUpImpulse = 5f;
    [SerializeField] private float _chargeBumpRadius = 2.5f;
    [SerializeField] private float _squashSeconds = 2f;
    [Tooltip("Rayon d'écrasement : passer à cette distance d'un joueur plus petit l'aplatit (multiplié par la taille du géant)")]
    [SerializeField] private float _crushRadius = 2f;
    [SerializeField] private float _iceControlAccel = 8f;

    [Tooltip("Son joué chez la VICTIME quand elle se fait confondre (vertigo)")]
    [SerializeField] private AudioClip _confusionOwnClip;

    private bool _wasConfused;

    [Networked] private TickTimer SlowTimer { get; set; }
    [Networked] private TickTimer SpeedBuffTimer { get; set; }
    [Networked] private TickTimer ForceBuffTimer { get; set; }
    [Networked] private TickTimer InvisibilityTimer { get; set; }
    [Networked] private TickTimer KnockbackTimer { get; set; }
    [Networked] private Vector3 KnockbackVelocity { get; set; }
    [Networked] private float KnockbackSeconds { get; set; }
    [Networked] private TickTimer ConfusionTimer { get; set; }
    [Networked] private TickTimer ShrinkTimer { get; set; }
    [Networked] private TickTimer StunTimer { get; set; }
    [Networked] private TickTimer ChargeTimer { get; set; }
    [Networked] private TickTimer SquashTimer { get; set; }
    [Networked] private TickTimer SquashCooldownTimer { get; set; }
    [Networked] private TickTimer OnIceTimer { get; set; }
    [Networked] private Vector3 IceSlideVelocity { get; set; }

    private Renderer[] _renderers;
    private AudioSource _voiceSource;
    private bool _voiceSourceSearched;
    private GUIStyle _statusStyle;
    private readonly System.Collections.Generic.List<string> _statusLines = new System.Collections.Generic.List<string>(8);

    public bool IsSlowed => Object != null && Object.IsValid && SlowTimer.ExpiredOrNotRunning(Runner) == false;
    public bool HasSpeedBuff => Object != null && Object.IsValid && SpeedBuffTimer.ExpiredOrNotRunning(Runner) == false;
    public bool HasForceBuff => Object != null && Object.IsValid && ForceBuffTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsInvisible => Object != null && Object.IsValid && InvisibilityTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsConfused => Object != null && Object.IsValid && ConfusionTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsShrunk => Object != null && Object.IsValid && ShrinkTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsCharging => Object != null && Object.IsValid && ChargeTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsSquashed => Object != null && Object.IsValid && SquashTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsSquashProtected => Object != null && Object.IsValid && SquashCooldownTimer.ExpiredOrNotRunning(Runner) == false;
    public bool IsOnIce => Object != null && Object.IsValid && OnIceTimer.ExpiredOrNotRunning(Runner) == false;

    // Paralysé par electra OU aplati par un écrasement
    public bool IsStunned => Object != null && Object.IsValid &&
        (StunTimer.ExpiredOrNotRunning(Runner) == false || IsSquashed);

    // Géant (2) > normal (1) > rétréci (0) : on n'écrase que plus petit que soi
    public int SizeRank => IsShrunk ? 0 : HasForceBuff ? 2 : 1;

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

    public float ForceMultiplier
    {
        get
        {
            float multiplier = 1f;
            if (HasForceBuff)
                multiplier *= _forceBuffMultiplier;
            if (IsShrunk)
                multiplier *= _shrinkForceMultiplier;
            return multiplier;
        }
    }

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

    public void ApplyConfusion(float seconds)
    {
        ConfusionTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public void ApplyStun(float seconds)
    {
        StunTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    public void ApplyShrink(float seconds)
    {
        ShrinkTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    /// <summary>Purge tous les effets actifs (retour au lobby). Serveur uniquement.</summary>
    public void ClearAllEffects()
    {
        SlowTimer = default;
        SpeedBuffTimer = default;
        ForceBuffTimer = default;
        InvisibilityTimer = default;
        ConfusionTimer = default;
        ShrinkTimer = default;
        StunTimer = default;
        KnockbackTimer = default;
        ChargeTimer = default;
        SquashTimer = default;
        SquashCooldownTimer = default;
        OnIceTimer = default;
        IceSlideVelocity = default;
    }

    /// <summary>Charge taurus : dash en avant, les percutés sont éjectés (voir FixedUpdateNetwork).</summary>
    public void ApplyCharge(Vector3 direction, float speed = 24f, float seconds = 0.35f)
    {
        ApplyKnockback(direction * speed, seconds);
        ChargeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    /// <summary>Écrasé par un joueur plus grand : aplati + immobile.</summary>
    public void ApplySquash(float seconds)
    {
        SquashTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        // Grâce après l'écrasement, sinon l'attaquant resté dessus ré-écrase en boucle
        SquashCooldownTimer = TickTimer.CreateFromSeconds(Runner, seconds + 1.5f);
    }

    /// <summary>Marque le joueur comme étant sur la glace (rafraîchi par IceZone).</summary>
    public void ApplyOnIce(float seconds = 0.3f)
    {
        OnIceTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    /// <summary>Glisse : la vélocité dérive lentement vers l'input au lieu de le suivre.</summary>
    public Vector3 UpdateIceSlide(Vector3 desiredVelocity, float deltaTime)
    {
        Vector3 slide = Vector3.MoveTowards(IceSlideVelocity, desiredVelocity, _iceControlAccel * deltaTime);
        IceSlideVelocity = slide;
        return slide;
    }

    /// <summary>Hors glace : mémorise la vélocité courante (élan à l'entrée sur la glace).</summary>
    public void SyncIceSlide(Vector3 currentVelocity)
    {
        IceSlideVelocity = currentVelocity;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
            return;

        if (IsCharging)
            ChargeBump();

        CheckCrush();
    }

    // Taurus : percute joueurs et objets sur le passage pendant la charge.
    // Les joueurs PROCHES du dash sont aussi bousculés (moins fort avec la distance).
    private void ChargeBump()
    {
        var hits = Physics.OverlapSphere(transform.position + Vector3.up, _chargeBumpRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var other = hit.GetComponentInParent<PlayerSpellEffects>();
            if (other != null)
            {
                if (other == this || other.Object == null || other.Object.IsValid == false)
                    continue;

                Vector3 away = other.transform.position - transform.position;
                away.y = 0f;
                float distance = away.magnitude;

                // Pleine puissance au contact, moitié en bord de rayon
                float falloff = 1f - Mathf.Clamp01(distance / _chargeBumpRadius) * 0.5f;

                Vector3 knockback = (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * (_chargeBumpImpulse * falloff);
                knockback.y = _chargeBumpUpImpulse * falloff;
                other.ApplyKnockback(knockback);
                continue;
            }

            var rigidbody = hit.attachedRigidbody;
            if (rigidbody != null && rigidbody.isKinematic == false)
            {
                Vector3 away = (rigidbody.worldCenterOfMass - transform.position).normalized;
                rigidbody.AddForce(away * _chargeBumpImpulse, ForceMode.Impulse);
            }
        }
    }

    // Écrasement : un joueur plus grand posé sur un plus petit l'aplatit (stun)
    private void CheckCrush()
    {
        if (IsSquashed)
            return;

        int myRank = SizeRank;
        if (myRank == 0)
            return;

        foreach (var other in FindObjectsByType<PlayerSpellEffects>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (other == this || other.Object == null || other.Object.IsValid == false)
                continue;
            if (other.SizeRank >= myRank || other.IsSquashed || other.IsSquashProtected)
                continue;

            Vector3 delta = transform.position - other.transform.position;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            float victimHeight = 1.8f * Mathf.Max(0.2f, other.transform.localScale.y);
            float crushRadius = _crushRadius * Mathf.Max(1f, transform.localScale.x);

            // Zone de proximité : passer à côté (ou dessus) d'un plus petit l'écrase,
            // pas besoin de contact direct — juste être à peu près au même niveau
            if (horizontal < crushRadius && delta.y > -1.5f && delta.y < victimHeight + 2f)
                other.ApplySquash(_squashSeconds);
        }
    }

    public void ApplyKnockback(Vector3 velocity, float seconds = 0.35f)
    {
        KnockbackVelocity = velocity;
        KnockbackSeconds = seconds;
        KnockbackTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    /// <summary>Vrai uniquement pendant le premier tick de l'éjection (impulsion verticale one-shot).</summary>
    public bool IsKnockbackFresh(float tickDelta)
    {
        if (Object == null || Object.IsValid == false || KnockbackTimer.ExpiredOrNotRunning(Runner))
            return false;

        float remaining = KnockbackTimer.RemainingTime(Runner) ?? 0f;
        return KnockbackSeconds - remaining <= tickDelta;
    }

    public override void Render()
    {
        // Vertigo : son local chez la victime au moment où la confusion démarre
        if (HasInputAuthority)
        {
            bool confusedNow = IsConfused;
            if (confusedNow && _wasConfused == false && _confusionOwnClip != null && Camera.main != null)
                AudioSource.PlayClipAtPoint(_confusionOwnClip, Camera.main.transform.position, 0.5f);
            _wasConfused = confusedNow;
        }

        // Taille : minima rétrécit, maximus grandit (cumulables), appliqué chez
        // tout le monde y compris le joueur touché (sa caméra suit, effet immersif).
        // Écrasé : aplati au sol (la caméra descend avec le pivot, puis remonte).
        float baseScale = 1f;
        if (IsShrunk)
            baseScale *= _shrinkScale;
        if (HasForceBuff)
            baseScale *= _growScale;

        Vector3 targetScale = IsSquashed
            ? new Vector3(baseScale * 1.35f, baseScale * 0.12f, baseScale * 1.35f)
            : Vector3.one * baseScale;

        // L'aplatissement est rapide, le retour à la normale plus doux
        float speed = IsSquashed ? 12f : 3f;
        transform.localScale = new Vector3(
            Mathf.MoveTowards(transform.localScale.x, targetScale.x, Time.deltaTime * speed),
            Mathf.MoveTowards(transform.localScale.y, targetScale.y, Time.deltaTime * speed * 2f),
            Mathf.MoveTowards(transform.localScale.z, targetScale.z, Time.deltaTime * speed));

        // Voix aiguë tant que le joueur est rétréci : on pitche l'AudioSource du
        // Speaker vocal Photon (chaque client joue les voix distantes localement,
        // donc tout le monde entend l'effet)
        if (_voiceSourceSearched == false)
        {
            _voiceSourceSearched = true;
            var speaker = GetComponentInChildren<Photon.Voice.Unity.Speaker>(true);
            if (speaker != null)
                _voiceSource = speaker.GetComponent<AudioSource>();
        }

        if (_voiceSource != null)
        {
            // minima → aigu, maximus → grave (cumulables)
            float targetPitch = 1f;
            if (IsShrunk)
                targetPitch *= _shrinkVoicePitch;
            if (HasForceBuff)
                targetPitch *= _growVoicePitch;

            if (Mathf.Abs(_voiceSource.pitch - targetPitch) > 0.01f)
                _voiceSource.pitch = targetPitch;
        }

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

    // UI LOCALE : badges des effets actifs sur soi (design system :
    // h 40, r 10, ◆ + nom + temps, barre 3 px couleur du sort)

    private readonly System.Collections.Generic.List<(string label, float remaining, Color color)> _statusBadges =
        new System.Collections.Generic.List<(string, float, Color)>(8);

    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || HasInputAuthority == false)
            return;

        UITheme.Begin();

        if (_statusStyle == null)
            _statusStyle = UITheme.Label(UITheme.BodyBold, 17, UITheme.Parchment, TextAnchor.MiddleLeft);

        _statusBadges.Clear();
        AppendStatus(SlowTimer, "Slowed", SpellWords.ColorOf(0));          // polaris
        AppendStatus(SpeedBuffTimer, "Speed +", SpellWords.ColorOf(2));   // aurora
        AppendStatus(ForceBuffTimer, "Strength +", SpellWords.ColorOf(3));     // maximus
        AppendStatus(InvisibilityTimer, "Invisible", SpellWords.ColorOf(4)); // anima
        AppendStatus(ConfusionTimer, "Confused", SpellWords.ColorOf(5));   // vertigo
        AppendStatus(ShrinkTimer, "Shrunk", SpellWords.ColorOf(6));        // minima
        AppendStatus(StunTimer, "Shocked", SpellWords.ColorOf(7));       // electra
        AppendStatus(SquashTimer, "Squashed", SpellWords.ColorOf(8));        // gris pierre

        if (_statusBadges.Count == 0)
            return;

        // Sous le pill « SALLE » affiché par le MatchHUD en haut à gauche
        float y = 72f;
        foreach (var (label, remaining, color) in _statusBadges)
        {
            var content = new GUIContent($"◆  {label}");
            float textWidth = _statusStyle.CalcSize(content).x;
            var badge = new Rect(32f, y, textWidth + 76f, 40f);

            UITheme.DrawPanel(badge, 10f);

            _statusStyle.normal.textColor = color;
            GUI.Label(new Rect(badge.x + 14f, badge.y, textWidth + 10f, 37f), content, _statusStyle);
            _statusStyle.normal.textColor = UITheme.Parchment;
            GUI.Label(new Rect(badge.x + textWidth + 28f, badge.y, 44f, 37f), $"{remaining:0} s", _statusStyle);

            // Barre 3 px couleur du sort en bas du badge
            UITheme.DrawRounded(new Rect(badge.x + 6f, badge.yMax - 4f, badge.width - 12f, 3f), color, 1.5f);

            y += 48f;
        }
    }

    private void AppendStatus(TickTimer timer, string label, Color color)
    {
        if (timer.ExpiredOrNotRunning(Runner))
            return;

        float remaining = timer.RemainingTime(Runner) ?? 0f;
        _statusBadges.Add((label, remaining, color));
    }
}
