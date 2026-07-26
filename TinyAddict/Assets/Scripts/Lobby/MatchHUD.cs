using UnityEngine;

/// <summary>
/// HUD de match (design system dark fantasy) : chrono + jauges des deux équipes
/// + ligne zones en haut-centre, roster en haut-droite, écran de victoire à la
/// fin du chrono, overlay spectateur. S'auto-instancie, aucun setup nécessaire.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    private float _nextRefresh;
    private PlayerProfile _localProfile;
    private PlayerProfile[] _profiles = new PlayerProfile[0];
    private Fusion.NetworkRunner _runner;

    private GUIStyle _timerStyle;
    private GUIStyle _capsStyle;
    private GUIStyle _gaugeLabelStyle;
    private GUIStyle _zoneLineStyle;
    private GUIStyle _rosterStyle;
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

        if (_runner == null || _runner.IsRunning == false)
        {
            _runner = null;
            var enumerator = Fusion.NetworkRunner.GetInstancesEnumerator();
            while (enumerator.MoveNext())
            {
                var candidate = enumerator.Current;
                if (candidate != null && candidate.IsRunning)
                {
                    _runner = candidate;
                    break;
                }
            }
        }

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

        UITheme.Begin();
        EnsureStyles();

        if (gameState.IsEnded)
        {
            DrawEndScreen(gameState);
            return;
        }

        DrawTopCenter(gameState);
        DrawRoomCode();
        DrawTeamRosters();
        DrawSpectatorHint();
    }

    // Code de la salle en haut à gauche (au-dessus des badges d'effets)
    private void DrawRoomCode()
    {
        if (_runner == null || _runner.SessionInfo.IsValid == false)
            return;

        string roomName = _runner.SessionInfo.Name;
        if (string.IsNullOrEmpty(roomName))
            return;

        var content = new GUIContent($"SALLE  <color=#{ColorUtility.ToHtmlStringRGB(UITheme.Gold)}><b>{roomName}</b></color>");
        float width = _capsStyle.CalcSize(content).x + 28f;
        var pill = new Rect(32f, 28f, width, 32f);

        UITheme.DrawPanel(pill, 16f);
        _capsStyle.normal.textColor = UITheme.TextDim;
        GUI.Label(new Rect(pill.x + 14f, pill.y, width - 28f, pill.height), content, _capsStyle);
    }

    // HAUT-CENTRE : chrono → jauges → ligne zones (marge haute 28 px)

    private void DrawTopCenter(GameState gameState)
    {
        float centerX = UITheme.VirtualWidth * 0.5f;

        // Chrono (danger sous 30 s, pulse)
        float remaining = Mathf.Max(0f, gameState.RemainingSeconds);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);

        var timerRect = new Rect(centerX - 80f, 28f, 160f, 56f);
        UITheme.DrawPanel(timerRect);
        bool danger = remaining <= 30f;
        _timerStyle.normal.textColor = danger
            ? UITheme.WithAlpha(UITheme.Danger, 0.65f + Mathf.PingPong(Time.time, 0.35f))
            : UITheme.Parchment;
        GUI.Label(timerRect, $"{minutes:0}:{seconds:00}", _timerStyle);

        // Jauges des deux équipes (la nôtre à gauche)
        Team localTeam = _localProfile != null ? _localProfile.Team : Team.None;
        bool localIsBlue = localTeam == Team.Blue;

        var leftPanel = new Rect(centerX - 470f, 96f, 450f, 64f);
        var rightPanel = new Rect(centerX + 20f, 96f, 450f, 64f);

        DrawGaugePanel(leftPanel, localIsBlue ? Team.Blue : Team.Red, gameState,
            localTeam == Team.None ? null : "VOTRE ÉQUIPE");
        DrawGaugePanel(rightPanel, localIsBlue ? Team.Red : Team.Blue, gameState, null);

        // Ligne zones (urgence orange sous 10 s)
        if (gameState.CurrentStep < gameState.ZoneStepsCount - 1)
        {
            float nextMoveIn = gameState.StepSeconds - gameState.ElapsedSeconds % gameState.StepSeconds;
            var zoneRect = new Rect(centerX - 260f, 170f, 520f, 34f);
            UITheme.DrawPanel(zoneRect);

            if (nextMoveIn <= 10f)
            {
                _zoneLineStyle.normal.textColor = UITheme.WithAlpha(UITheme.Urgency, 0.65f + Mathf.PingPong(Time.time, 0.35f));
                GUI.Label(zoneRect, $"⚠ Les zones bougent dans {nextMoveIn:0} s !", _zoneLineStyle);
            }
            else
            {
                _zoneLineStyle.normal.textColor = UITheme.TextDim;
                GUI.Label(zoneRect, $"Zones : étape {gameState.CurrentStep + 1}/{gameState.ZoneStepsCount} — déplacement dans {nextMoveIn:0} s", _zoneLineStyle);
            }
        }
    }

    private void DrawGaugePanel(Rect rect, Team team, GameState gameState, string tag)
    {
        UITheme.DrawPanel(rect);

        Color teamColor = PlayerProfile.ColorOfTeam(team);
        float charge = team == Team.Red ? gameState.RedPercent : gameState.BluePercent;
        int objects = team == Team.Red ? gameState.RedCollected.Count : gameState.BlueCollected.Count;
        string name = team == Team.Red ? "Rouge" : "Bleu";

        _gaugeLabelStyle.normal.textColor = UITheme.PseudoColor(team);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 6f, rect.width - 140f, 24f),
            $"{name}  <b>{charge * 100f:0} %</b>  <size=15>({objects} obj)</size>", _gaugeLabelStyle);

        if (string.IsNullOrEmpty(tag) == false)
        {
            _capsStyle.normal.textColor = UITheme.TextDim;
            var tagStyle = new GUIStyle(_capsStyle) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(rect.xMax - 160f, rect.y + 8f, 142f, 20f), tag, tagStyle);
        }

        UITheme.DrawGauge(new Rect(rect.x + 18f, rect.y + 36f, rect.width - 36f, 18f), charge, teamColor);
    }

    // HAUT-DROITE : roster des deux équipes (sous le pill micro)

    private void DrawTeamRosters()
    {
        var lines = new System.Collections.Generic.List<(string text, Color color)>();

        AppendTeam(lines, Team.Red, "CLAN ROUGE");
        AppendTeam(lines, Team.Blue, "CLAN BLEU");

        const float rowHeight = 26f;
        float panelWidth = 200f;
        float panelHeight = lines.Count * rowHeight + 18f;
        var panel = new Rect(UITheme.VirtualWidth - panelWidth - 32f, 76f, panelWidth, panelHeight);
        UITheme.DrawPanel(panel);

        float y = panel.y + 9f;
        foreach (var (text, color) in lines)
        {
            _rosterStyle.normal.textColor = color;
            GUI.Label(new Rect(panel.x + 16f, y, panelWidth - 32f, rowHeight), text, _rosterStyle);
            y += rowHeight;
        }
    }

    private void AppendTeam(System.Collections.Generic.List<(string, Color)> lines, Team team, string header)
    {
        lines.Add((header, UITheme.TextDim));

        bool any = false;
        foreach (var profile in _profiles)
        {
            if (profile == null || profile.Object == null || profile.Object.IsValid == false || profile.Team != team)
                continue;

            string marker = profile == _localProfile ? "  <size=13>vous</size>" : "";
            lines.Add(($"■ {profile.Nickname}{marker}", UITheme.PseudoColor(team)));
            any = true;
        }

        if (any == false)
            lines.Add(("■ personne", UITheme.WithAlpha(UITheme.TextDim, 0.5f)));
    }

    // SPECTATEUR

    private void DrawSpectatorHint()
    {
        if (SpectatorController.IsActive == false)
            return;

        var rect = new Rect(UITheme.VirtualWidth * 0.5f - 230f, UITheme.VirtualHeight - 64f, 460f, 34f);
        UITheme.DrawPanel(rect);
        _capsStyle.normal.textColor = UITheme.TextDim;
        var centered = new GUIStyle(_capsStyle) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(rect, "SPECTATEUR — ZQSD · ESPACE ↑ · CTRL ↓ · SHIFT VITE", centered);
    }

    // FIN DE PARTIE

    private void DrawEndScreen(GameState gameState)
    {
        GUI.color = UITheme.WithAlpha(UITheme.LobbyDim, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, UITheme.VirtualWidth, UITheme.VirtualHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float centerX = UITheme.VirtualWidth * 0.5f;

        string title;
        Color titleColor;
        if (gameState.WinnerTeam == (int)Team.Red)
        {
            title = "LE CLAN ROUGE GAGNE !";
            titleColor = UITheme.TeamRed;
        }
        else if (gameState.WinnerTeam == (int)Team.Blue)
        {
            title = "LE CLAN BLEU GAGNE !";
            titleColor = UITheme.TeamBlue;
        }
        else
        {
            title = "ÉGALITÉ !";
            titleColor = UITheme.Parchment;
        }

        _endTitleStyle.normal.textColor = titleColor;
        GUI.Label(new Rect(0f, 300f, UITheme.VirtualWidth, 150f), title, _endTitleStyle);

        // Jauges finales (spec : 820 px int., h 36)
        DrawFinalGauge(new Rect(centerX - 410f, 520f, 820f, 36f), Team.Red, gameState.RedPercent);
        DrawFinalGauge(new Rect(centerX - 410f, 580f, 820f, 36f), Team.Blue, gameState.BluePercent);

        // Retour automatique à la salle d'attente
        float returnIn = gameState.LobbyReturnRemaining;
        if (returnIn > 0f)
        {
            _endSubtitleStyle.normal.textColor = UITheme.TextDim;
            GUI.Label(new Rect(0f, 680f, UITheme.VirtualWidth, 32f),
                $"Retour au lobby dans {returnIn:0} s…", _endSubtitleStyle);
        }
    }

    private void DrawFinalGauge(Rect rect, Team team, float charge)
    {
        Color teamColor = PlayerProfile.ColorOfTeam(team);
        UITheme.DrawGauge(rect, charge, teamColor);

        _endSubtitleStyle.normal.textColor = UITheme.PseudoColor(team);
        var left = new GUIStyle(_endSubtitleStyle) { alignment = TextAnchor.MiddleLeft };
        GUI.Label(new Rect(rect.x - 130f, rect.y, 120f, rect.height), team == Team.Red ? "Rouge" : "Bleu",
            new GUIStyle(left) { alignment = TextAnchor.MiddleRight });
        GUI.Label(new Rect(rect.xMax + 14f, rect.y, 140f, rect.height), $"{charge * 100f:0} %", left);
    }

    private void EnsureStyles()
    {
        if (_timerStyle != null)
            return;

        _timerStyle = UITheme.Label(UITheme.Display, 44, UITheme.Parchment, TextAnchor.MiddleCenter);
        _capsStyle = UITheme.Label(UITheme.BodyExtraBold, 14, UITheme.TextDim, TextAnchor.MiddleLeft);
        _gaugeLabelStyle = UITheme.Label(UITheme.BodyBold, 19, UITheme.Parchment, TextAnchor.MiddleLeft);
        _zoneLineStyle = UITheme.Label(UITheme.BodyBold, 17, UITheme.TextDim, TextAnchor.MiddleCenter);
        _rosterStyle = UITheme.Label(UITheme.BodyBold, 16, UITheme.Parchment, TextAnchor.MiddleLeft);
        _endTitleStyle = UITheme.Label(UITheme.Display, 110, UITheme.Parchment, TextAnchor.MiddleCenter);
        _endSubtitleStyle = UITheme.Label(UITheme.BodyBold, 24, UITheme.Parchment, TextAnchor.MiddleCenter);
    }
}
