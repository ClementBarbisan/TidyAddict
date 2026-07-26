using Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Touche M : mute/unmute du micro (chat vocal). Affiche l'état en haut à droite.
/// L'incantation d'un sort peut forcer temporairement la transmission via
/// <see cref="ForceTransmit"/> pour que les autres entendent le mot prononcé.
/// S'auto-instancie, aucun setup nécessaire.
/// </summary>
public class MicMuteControl : MonoBehaviour
{
    public static bool IsMuted { get; private set; }

    /// <summary>Forcé à true pendant une incantation pour outrepasser le mute.</summary>
    public static bool ForceTransmit { get; set; }

    private const float FindRecorderInterval = 1f;

    private Recorder _recorder;
    private float _nextFindTime;
    private GUIStyle _labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("MicMuteControl");
        DontDestroyOnLoad(go);
        go.AddComponent<MicMuteControl>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
        {
            IsMuted = !IsMuted;
        }

        if (Time.unscaledTime >= _nextFindTime)
        {
            _nextFindTime = Time.unscaledTime + FindRecorderInterval;

            // Tant que le Recorder en main n'est pas celui d'un runner démarré,
            // on re-cherche (le runner de la scène n'est qu'un template).
            var currentRunner = _recorder != null ? _recorder.GetComponent<NetworkRunner>() : null;
            if (currentRunner == null || currentRunner.IsRunning == false)
                _recorder = FindActiveRecorder();
        }

        if (_recorder != null)
        {
            bool shouldTransmit = ForceTransmit || IsMuted == false;
            if (_recorder.TransmitEnabled != shouldTransmit)
                _recorder.TransmitEnabled = shouldTransmit;
        }
    }

    private static Recorder FindActiveRecorder()
    {
        // Le runner de la scène sert de template à FusionBootstrap :
        // on privilégie le Recorder du runner réellement démarré.
        var recorders = FindObjectsByType<Recorder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Recorder fallback = null;

        foreach (var recorder in recorders)
        {
            fallback = recorder;
            var runner = recorder.GetComponent<NetworkRunner>();
            if (runner != null && runner.IsRunning)
                return recorder;
        }

        return fallback;
    }

    private void OnGUI()
    {
        UITheme.Begin();

        if (_labelStyle == null)
            _labelStyle = UITheme.Label(UITheme.BodyExtraBold, 14, UITheme.Parchment, TextAnchor.MiddleLeft);

        // Pill micro (design system : r999, dot 12 px vert = actif / rouge = coupé)
        string text = IsMuted ? "MIC MUTED" : "MIC ON";
        Color dotColor = IsMuted ? UITheme.Danger : UITheme.Success;

        var content = new GUIContent(text);
        float textWidth = _labelStyle.CalcSize(content).x;
        float width = textWidth + 78f;
        var pill = new Rect(UITheme.VirtualWidth - width - 32f, 28f, width, 36f);

        UITheme.DrawRounded(pill, UITheme.WithAlpha(UITheme.PanelHud, 0.85f), 18f);
        UITheme.DrawBorder(pill, UITheme.WithAlpha(UITheme.Brass, 0.55f), 1.5f, 18f);

        UITheme.DrawRounded(new Rect(pill.x + 14f, pill.y + 12f, 12f, 12f), dotColor, 6f);
        GUI.Label(new Rect(pill.x + 34f, pill.y, textWidth + 8f, pill.height), content, _labelStyle);

        // Touche M dans un petit cadre
        var keyRect = new Rect(pill.xMax - 32f, pill.y + 7f, 22f, 22f);
        UITheme.DrawBorder(keyRect, UITheme.WithAlpha(UITheme.TextDim, 0.6f), 1.5f, 6f);
        var keyStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        keyStyle.normal.textColor = UITheme.TextDim;
        GUI.Label(keyRect, "M", keyStyle);
    }
}
