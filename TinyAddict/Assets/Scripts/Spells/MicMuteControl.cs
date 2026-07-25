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
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        string text = IsMuted
            ? "<color=red>Micro muté</color> (M)"
            : "<color=lime>Micro activé</color> (M)";

        var content = new GUIContent(text);
        float width = _labelStyle.CalcSize(content).x + 16f;
        var rect = new Rect(Screen.width - width - 12f, 12f, width, 26f);

        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.Label(rect, content, _labelStyle);
    }
}
