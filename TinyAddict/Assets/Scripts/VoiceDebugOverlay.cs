using System.Collections.Generic;
using System.Text;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Overlay de debug du vocal (toggle F3) : état de la connexion voix, micro détecté,
/// niveau d'entrée, détection de voix, et qui parle/transmet.
/// S'auto-instancie dans toutes les scènes, aucun setup nécessaire.
/// </summary>
public class VoiceDebugOverlay : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    private bool _visible = true;
    private float _nextRefresh;

    private VoiceConnection _voiceConnection;
    private Recorder _recorder;
    private VoiceNetworkObject[] _voiceObjects = new VoiceNetworkObject[0];

    private readonly StringBuilder _sb = new StringBuilder(512);
    private GUIStyle _boxStyle;
    private Texture2D _barTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("VoiceDebugOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<VoiceDebugOverlay>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
        {
            _visible = !_visible;
        }

        if (_visible == false || Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + RefreshInterval;
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        // Le runner de la scène sert de template à FusionBootstrap : plusieurs
        // VoiceConnection peuvent coexister, on prend celle du runner actif.
        _voiceConnection = null;
        _recorder = null;

        var connections = FindObjectsByType<VoiceConnection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var connection in connections)
        {
            if (_voiceConnection == null)
                _voiceConnection = connection;

            var runner = connection.GetComponent<Fusion.NetworkRunner>();
            if (runner != null && runner.IsRunning)
            {
                _voiceConnection = connection;
                break;
            }
        }

        if (_voiceConnection != null)
            _recorder = _voiceConnection.PrimaryRecorder;

        _voiceObjects = FindObjectsByType<VoiceNetworkObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private void OnGUI()
    {
        if (_visible == false)
            return;

        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8),
            };
            _barTexture = Texture2D.whiteTexture;
        }

        _sb.Length = 0;
        _sb.AppendLine("<b>VOICE DEBUG</b> (F3 pour masquer)");
        AppendConnectionInfo();
        AppendMicrophoneInfo();
        AppendSpeakersInfo();

        var content = new GUIContent(_sb.ToString());
        var size = _boxStyle.CalcSize(content);
        float width = Mathf.Max(340f, size.x);
        var rect = new Rect(10f, 10f, width, size.y);

        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, _barTexture);
        GUI.color = previousColor;

        GUI.Label(rect, content, _boxStyle);

        DrawMicLevelBar(new Rect(rect.x + 10f, rect.yMax + 4f, width - 20f, 8f));
    }

    private void AppendConnectionInfo()
    {
        if (_voiceConnection == null)
        {
            _sb.AppendLine(Colored("Voice : aucune VoiceConnection active", "red"));
            return;
        }

        string state = _voiceConnection.ClientState.ToString();
        string color = state == "Joined" ? "lime" : "yellow";
        _sb.AppendLine($"Voice : {Colored(state, color)}   (rx {_voiceConnection.FramesReceivedPerSecond:F0} f/s)");
    }

    private void AppendMicrophoneInfo()
    {
        var devices = Microphone.devices;
        if (devices.Length == 0)
        {
            _sb.AppendLine(Colored("Micro : AUCUN détecté — vérifier les droits Windows !", "red"));
            return;
        }

        if (_recorder == null)
        {
            _sb.AppendLine(Colored($"Micro : {devices.Length} détecté(s), pas de Recorder actif", "yellow"));
            return;
        }

        _sb.AppendLine($"Micro : {_recorder.MicrophoneDevice} ({devices.Length} détecté(s))");

        _sb.Append("Recorder : ");
        _sb.Append(_recorder.RecordingEnabled ? Colored("capture ON", "lime") : Colored("capture OFF", "red"));
        _sb.Append("  ");
        _sb.Append(_recorder.TransmitEnabled ? Colored("transmit ON", "lime") : Colored("transmit OFF", "red"));
        _sb.Append("  ");
        _sb.AppendLine(_recorder.IsCurrentlyTransmitting ? Colored("● ÉMET", "lime") : Colored("○ silence", "grey"));

        if (_recorder.VoiceDetection)
        {
            float avg = _recorder.LevelMeter != null ? _recorder.LevelMeter.CurrentAvgAmp : 0f;
            bool aboveThreshold = avg >= _recorder.VoiceDetectionThreshold;
            _sb.AppendLine($"Détection voix : ON (seuil {_recorder.VoiceDetectionThreshold:F3}) " +
                           (aboveThreshold ? Colored("→ au-dessus", "lime") : Colored("→ en dessous", "grey")));
        }
        else
        {
            _sb.AppendLine("Détection voix : OFF (micro ouvert en continu)");
        }
    }

    private void AppendSpeakersInfo()
    {
        _sb.AppendLine($"Joueurs ({_voiceObjects.Length}) :");
        foreach (var voiceObject in _voiceObjects)
        {
            if (voiceObject == null || voiceObject.Object == null || voiceObject.Object.IsValid == false)
                continue;

            string label = $"  P{voiceObject.Object.InputAuthority.PlayerId}";
            if (voiceObject.IsLocal)
            {
                string status = voiceObject.IsRecording ? Colored("● parle", "lime") : Colored("○ muet", "grey");
                _sb.AppendLine($"{label} (moi)  {status}");
            }
            else
            {
                bool linked = voiceObject.SpeakerInUse != null && voiceObject.SpeakerInUse.IsLinked;
                string status = voiceObject.IsSpeaking ? Colored("● parle", "lime")
                              : linked ? Colored("○ silencieux", "grey")
                              : Colored("speaker non lié !", "red");
                _sb.AppendLine($"{label}  {status}");
            }
        }
    }

    private void DrawMicLevelBar(Rect rect)
    {
        if (_recorder == null || _recorder.LevelMeter == null)
            return;

        var previousColor = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, _barTexture);

        // Amplitude ~[0..1], la voix normale reste faible : échelle x10 pour la lisibilité
        float level = Mathf.Clamp01(_recorder.LevelMeter.CurrentPeakAmp * 10f);
        GUI.color = _recorder.IsCurrentlyTransmitting ? Color.green : Color.grey;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * level, rect.height), _barTexture);

        // Repère du seuil de détection de voix
        if (_recorder.VoiceDetection)
        {
            float threshold = Mathf.Clamp01(_recorder.VoiceDetectionThreshold * 10f);
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(rect.x + rect.width * threshold - 1f, rect.y, 2f, rect.height), _barTexture);
        }

        GUI.color = previousColor;
    }

    private static string Colored(string text, string color)
    {
        return $"<color={color}>{text}</color>";
    }
}
