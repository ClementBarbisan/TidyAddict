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
                    _speaking.Add("Me");
                continue;
            }

            if (voiceObject.IsSpeaking == false)
                continue;

            // Pseudo coloré par équipe si le joueur a rempli son profil au lobby
            var profile = voiceObject.GetComponentInParent<PlayerProfile>();
            if (profile != null && profile.HasProfile)
            {
                string hex = UITheme.PseudoHex(profile.Team);
                float range = DefaultAudibleDistance;
                var profileSource = voiceObject.SpeakerInUse != null ? voiceObject.SpeakerInUse.GetComponent<AudioSource>() : null;
                if (profileSource != null)
                    range = profileSource.maxDistance;

                if (camera == null || Vector3.Distance(listenerPosition, voiceObject.transform.position) <= range)
                    _speaking.Add($"<color=#{hex}>{profile.Nickname}</color>");
                continue;
            }

            float audibleDistance = DefaultAudibleDistance;
            if (voiceObject.SpeakerInUse != null)
            {
                var source = voiceObject.SpeakerInUse.GetComponent<AudioSource>();
                if (source != null)
                    audibleDistance = source.maxDistance;
            }

            if (camera == null || Vector3.Distance(listenerPosition, voiceObject.transform.position) <= audibleDistance)
                _speaking.Add($"Player {voiceObject.Object.InputAuthority.PlayerId}");
        }
    }

    private void OnGUI()
    {
        if (_speaking.Count == 0)
            return;

        UITheme.Begin();

        if (_labelStyle == null)
            _labelStyle = UITheme.Label(UITheme.BodyBold, 17, UITheme.Parchment, TextAnchor.MiddleLeft);

        // Bas-gauche (design system) : pill par locuteur, ● succès + pseudo teinté
        const float rowHeight = 34f;
        float y = UITheme.VirtualHeight - 36f - _speaking.Count * (rowHeight + 6f);

        foreach (var name in _speaking)
        {
            var content = new GUIContent(name);
            float textWidth = _labelStyle.CalcSize(content).x;
            var pill = new Rect(32f, y, textWidth + 52f, rowHeight);

            UITheme.DrawRounded(pill, UITheme.WithAlpha(UITheme.PanelHud, 0.85f), 17f);
            UITheme.DrawBorder(pill, UITheme.WithAlpha(UITheme.Brass, 0.55f), 1.5f, 17f);

            // Point vert pulsant façon "voix active"
            float pulse = 0.6f + Mathf.PingPong(Time.time * 1.4f, 0.4f);
            UITheme.DrawRounded(new Rect(pill.x + 13f, pill.y + 11f, 12f, 12f), UITheme.WithAlpha(UITheme.Success, pulse), 6f);

            GUI.Label(new Rect(pill.x + 34f, pill.y, textWidth + 10f, pill.height), content, _labelStyle);
            y += rowHeight + 6f;
        }
    }
}
