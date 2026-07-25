using UnityEngine;

/// <summary>
/// HUD de match : chrono + % de collecte de son équipe et de l'équipe adverse
/// en haut de l'écran pendant la partie ; écran de victoire à la fin du chrono.
/// S'auto-instancie, aucun setup nécessaire.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    private float _nextRefresh;
    private PlayerProfile _localProfile;
    private PlayerProfile[] _profiles = new PlayerProfile[0];

    private GUIStyle _hudStyle;
    private GUIStyle _rosterStyle;
    private GUIStyle _timerStyle;
    private GUIStyle _endTitleStyle;
    private GUIStyle _endSubtitleStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("MatchHUD");
        DontDestroyOnLoad(go);
        go.AddComponent<MatchHUD>();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + RefreshInterval;

        _profiles = FindObjectsByType<PlayerProfile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (_localProfile == null)
        {
            foreach (var profile in _profiles)
            {
                if (profile.Object != null && profile.Object.IsValid && profile.HasInputAuthority)
                {
                    _localProfile = profile;
                    break;
                }
            }
        }
    }

    private void OnGUI()
    {
        var gameState = GameState.Instance;
        if (gameState == null || gameState.IsStarted == false)
            return;

        EnsureStyles();

        if (gameState.IsEnded)
        {
            DrawEndScreen(gameState);
            return;
        }

        DrawMatchHud(gameState);
    }

    private void DrawMatchHud(GameState gameState)
    {
        // Chrono au centre haut
        float remaining = Mathf.Max(0f, gameState.RemainingSeconds);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        string timerText = $"{minutes:0}:{seconds:00}";

        // Rouge quand il reste moins de 30 s
        string timerColored = remaining <= 30f ? $"<color=red>{timerText}</color>" : timerText;
        DrawWithBackground(new Rect(Screen.width * 0.5f - 50f, 8f, 100f, 34f), timerColored, _timerStyle);

        // % de son équipe et de l'équipe adverse
        Team localTeam = _localProfile != null ? _localProfile.Team : Team.None;

        string redHex = ColorUtility.ToHtmlStringRGB(PlayerProfile.RedTeamColor);
        string blueHex = ColorUtility.ToHtmlStringRGB(PlayerProfile.BlueTeamColor);
        string redLine = $"<color=#{redHex}>Rouge {gameState.RedPercent * 100f:0}%</color>";
        string blueLine = $"<color=#{blueHex}>Bleu {gameState.BluePercent * 100f:0}%</color>";

        string line;
        if (localTeam == Team.Red)
            line = $"Votre équipe : {redLine}   •   Adversaires : {blueLine}";
        else if (localTeam == Team.Blue)
            line = $"Votre équipe : {blueLine}   •   Adversaires : {redLine}";
        else
            line = $"{redLine}   •   {blueLine}";

        DrawWithBackground(new Rect(Screen.width * 0.5f - 260f, 46f, 520f, 28f), line, _hudStyle);

        DrawTeamRosters();
    }

    // Petit panneau avec la liste des joueurs connectés de chaque équipe
    private void DrawTeamRosters()
    {
        string redHex = ColorUtility.ToHtmlStringRGB(PlayerProfile.RedTeamColor);
        string blueHex = ColorUtility.ToHtmlStringRGB(PlayerProfile.BlueTeamColor);

        var lines = new System.Collections.Generic.List<string> { $"<color=#{redHex}><b>Équipe Rouge</b></color>" };
        AppendTeamMembers(lines, Team.Red);
        lines.Add($"<color=#{blueHex}><b>Équipe Bleue</b></color>");
        AppendTeamMembers(lines, Team.Blue);

        const float rowHeight = 20f;
        float panelWidth = 170f;
        float panelHeight = lines.Count * rowHeight + 10f;
        // Sous l'indicateur micro, en haut à droite
        var panel = new Rect(Screen.width - panelWidth - 12f, 46f, panelWidth, panelHeight);

        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previousColor;

        float y = panel.y + 5f;
        foreach (string rosterLine in lines)
        {
            GUI.Label(new Rect(panel.x + 8f, y, panelWidth - 16f, rowHeight), rosterLine, _rosterStyle);
            y += rowHeight;
        }
    }

    private void AppendTeamMembers(System.Collections.Generic.List<string> lines, Team team)
    {
        bool any = false;
        foreach (var profile in _profiles)
        {
            if (profile == null || profile.Object == null || profile.Object.IsValid == false)
                continue;
            if (profile.Team != team)
                continue;

            lines.Add($"  {profile.Nickname}");
            any = true;
        }

        if (any == false)
            lines.Add("  <i>personne</i>");
    }

    private void DrawEndScreen(GameState gameState)
    {
        // Fond assombri
        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        string title;
        if (gameState.WinnerTeam == (int)Team.Red)
        {
            string hex = ColorUtility.ToHtmlStringRGB(PlayerProfile.RedTeamColor);
            title = $"<color=#{hex}>L'ÉQUIPE ROUGE GAGNE !</color>";
        }
        else if (gameState.WinnerTeam == (int)Team.Blue)
        {
            string hex = ColorUtility.ToHtmlStringRGB(PlayerProfile.BlueTeamColor);
            title = $"<color=#{hex}>L'ÉQUIPE BLEUE GAGNE !</color>";
        }
        else
        {
            title = "ÉGALITÉ !";
        }

        string redHex = ColorUtility.ToHtmlStringRGB(PlayerProfile.RedTeamColor);
        string blueHex = ColorUtility.ToHtmlStringRGB(PlayerProfile.BlueTeamColor);
        string subtitle = $"<color=#{redHex}>Rouge {gameState.RedPercent * 100f:0}%</color>" +
                          $"   —   <color=#{blueHex}>Bleu {gameState.BluePercent * 100f:0}%</color>";

        GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 70f), title, _endTitleStyle);
        GUI.Label(new Rect(0f, Screen.height * 0.38f + 74f, Screen.width, 40f), subtitle, _endSubtitleStyle);
    }

    private void DrawWithBackground(Rect rect, string text, GUIStyle style)
    {
        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.Label(rect, text, style);
    }

    private void EnsureStyles()
    {
        if (_hudStyle != null)
            return;

        _hudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            richText = true,
            alignment = TextAnchor.MiddleCenter,
        };

        _rosterStyle = new GUIStyle(_hudStyle)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
        };

        _timerStyle = new GUIStyle(_hudStyle) { fontSize = 24 };
        _endTitleStyle = new GUIStyle(_hudStyle) { fontSize = 44 };
        _endSubtitleStyle = new GUIStyle(_hudStyle) { fontSize = 24 };
    }
}
