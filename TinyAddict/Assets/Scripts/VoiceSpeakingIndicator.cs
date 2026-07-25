using System.Collections.Generic;
using Photon.Voice.Fusion;
using UnityEngine;

/// <summary>
/// Affiche en bas à gauche le nom des joueurs en train de parler,
/// uniquement s'ils sont assez proches pour être audibles
/// (portée lue sur l'AudioSource de leur Speaker). Affiche aussi
/// « Moi » quand le micro local transmet. Aucun setup nécessaire.
/// </summary>
public class VoiceSpeakingIndicator : MonoBehaviour
{
    private const float RefreshInterval = 0.2f;
    private const float DefaultAudibleDistance = 25f;

    private float _nextRefresh;
    private readonly List<string> _speaking = new List<string>(8);

    private GUIStyle _labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("VoiceSpeakingIndicator");
        DontDestroyOnLoad(go);
        go.AddComponent<VoiceSpeakingIndicator>();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + RefreshInterval;
        _speaking.Clear();

        // L'AudioListener est sur la caméra (déplacée sur la tête du joueur local) :
        // c'est sa position qui détermine ce qu'on entend réellement.
        var camera = Camera.main;
        Vector3 listenerPosition = camera != null ? camera.transform.position : Vector3.zero;

        var voiceObjects = FindObjectsByType<VoiceNetworkObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var voiceObject in voiceObjects)
        {
            if (voiceObject == null || voiceObject.Object == null || voiceObject.Object.IsValid == false)
                continue;

            if (voiceObject.IsLocal)
            {
                if (voiceObject.IsRecording)
                    _speaking.Add("Moi");
                continue;
            }

            if (voiceObject.IsSpeaking == false)
                continue;

            float audibleDistance = DefaultAudibleDistance;
            if (voiceObject.SpeakerInUse != null)
            {
                var source = voiceObject.SpeakerInUse.GetComponent<AudioSource>();
                if (source != null)
                    audibleDistance = source.maxDistance;
            }

            if (camera == null || Vector3.Distance(listenerPosition, voiceObject.transform.position) <= audibleDistance)
                _speaking.Add($"Joueur {voiceObject.Object.InputAuthority.PlayerId}");
        }
    }

    private void OnGUI()
    {
        if (_speaking.Count == 0)
            return;

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 2, 2),
            };
        }

        const float rowHeight = 24f;
        float y = Screen.height - 16f - _speaking.Count * rowHeight;

        var previousColor = GUI.color;
        foreach (var name in _speaking)
        {
            var content = new GUIContent($"<color=lime>●</color> {name}");
            float width = _labelStyle.CalcSize(content).x;
            var rect = new Rect(16f, y, width, rowHeight);

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Label(rect, content, _labelStyle);
            y += rowHeight;
        }
    }
}
