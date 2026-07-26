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
    public bool IsStunned => Object != null && Object.IsValid && StunTimer.ExpiredOrNotRunning(Runner) == false;

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
        // Taille : minima rétrécit, maximus grandit (cumulables), appliqué chez
        // tout le monde y compris le joueur touché (sa caméra suit, effet immersif)
        float targetScale = 1f;
        if (IsShrunk)
            targetScale *= _shrinkScale;
        if (HasForceBuff)
            targetScale *= _growScale;

        float currentScale = transform.localScale.x;
        if (Mathf.Abs(currentScale - targetScale) > 0.001f)
        {
            transform.localScale = Vector3.one * Mathf.MoveTowards(currentScale, targetScale, Time.deltaTime * 2f);
        }

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
        AppendStatus(SlowTimer, "Ralenti", SpellWords.ColorOf(0));          // polaris
        AppendStatus(SpeedBuffTimer, "Vitesse +", SpellWords.ColorOf(2));   // aurora
        AppendStatus(ForceBuffTimer, "Force +", SpellWords.ColorOf(3));     // maximus
        AppendStatus(InvisibilityTimer, "Invisible", SpellWords.ColorOf(4)); // anima
        AppendStatus(ConfusionTimer, "Confusion", SpellWords.ColorOf(5));   // vertigo
        AppendStatus(ShrinkTimer, "Rétréci", SpellWords.ColorOf(6));        // minima
        AppendStatus(StunTimer, "Électrifié", SpellWords.ColorOf(7));       // electra

        if (_statusBadges.Count == 0)
            return;

        float y = 28f;
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
