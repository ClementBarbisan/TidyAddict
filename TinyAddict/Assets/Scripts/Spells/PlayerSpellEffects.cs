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

    // UI LOCALE : liste des effets actifs sur soi, avec le temps restant

    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || HasInputAuthority == false)
            return;

        if (_statusStyle == null)
        {
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 2, 2),
            };
        }

        _statusLines.Clear();
        AppendStatus(_statusLines, SlowTimer, "Ralenti", "#59BFFF");
        AppendStatus(_statusLines, SpeedBuffTimer, "Vitesse +", "#CC4DFF");
        AppendStatus(_statusLines, ForceBuffTimer, "Force +", "#FFBF00");
        AppendStatus(_statusLines, InvisibilityTimer, "Invisible", "#CCE6F2");
        AppendStatus(_statusLines, ConfusionTimer, "Confusion", "#FF59BF");
        AppendStatus(_statusLines, ShrinkTimer, "Rétréci", "#59FFA6");
        AppendStatus(_statusLines, StunTimer, "Électrifié", "#FFF233");

        if (_statusLines.Count == 0)
            return;

        const float rowHeight = 24f;
        float y = 12f;

        var previousColor = GUI.color;
        foreach (string statusLine in _statusLines)
        {
            var content = new GUIContent(statusLine);
            float width = _statusStyle.CalcSize(content).x;
            var rect = new Rect(12f, y, width, rowHeight);

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Label(rect, content, _statusStyle);
            y += rowHeight + 2f;
        }
    }

    private void AppendStatus(System.Collections.Generic.List<string> lines, TickTimer timer, string label, string hexColor)
    {
        if (timer.ExpiredOrNotRunning(Runner))
            return;

        float remaining = timer.RemainingTime(Runner) ?? 0f;
        lines.Add($"<color={hexColor}>{label}</color> {remaining:F0}s");
    }
}
