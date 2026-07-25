using Fusion;
using UnityEngine;

/// <summary>
/// Menu de lobby (design system dark fantasy) : choix du pseudo, puis du clan
/// (rouge/bleu, capacité 2/2), puis salle d'attente. L'hôte lance la partie à
/// 4 joueurs (ou force en debug). Si les deux clans sont pleins : spectateur.
/// Tant que le menu est ouvert, le curseur est libre : PlayerInput ignore
/// alors les entrées, le joueur est immobile. S'auto-instancie.
/// </summary>
public class LobbyMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private enum Step
    {
        Nickname,
        Connecting,
        Team,
        Waiting,
        Done,
    }

    private const float RefreshInterval = 0.5f;

    private Step _step = Step.Nickname;
    private string _nickname = "";
    private string _room = "Test";
    private int _team;
    private bool _profileSent;
    private bool _wantSpectator;
    private bool _spectatorStarted;
    private bool _cursorRestored;
    private float _nextRefresh;

    private NetworkRunner _runner;
    private PlayerProfile[] _profiles = new PlayerProfile[0];
    private PlayerProfile _localProfile;

    private GUIStyle _logoStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _labelCapsStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _fieldStyle;
    private GUIStyle _buttonTextStyle;
    private GUIStyle _cardTitleStyle;
    private GUIStyle _cardCountStyle;
    private GUIStyle _cardHintStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("LobbyMenu");
        DontDestroyOnLoad(go);
        go.AddComponent<LobbyMenu>();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            RefreshReferences();
        }

        // Envoi du profil dès que notre joueur est spawné
        if (_profileSent == false && _team != 0 && _localProfile != null)
        {
            _profileSent = true;
            _localProfile.RPC_SetProfile(_nickname, _team);
        }

        // Passage en spectateur : le serveur despawn notre personnage,
        // puis on active la caméra fantôme
        if (_wantSpectator && _spectatorStarted == false && _localProfile != null)
        {
            _spectatorStarted = true;
            _localProfile.RPC_BecomeSpectator();
            SpectatorController.Activate();
            _step = Step.Done;
        }

        // Connexion établie et notre joueur spawné : on passe au choix du clan
        if (_step == Step.Connecting && _runner != null && _runner.IsRunning && _localProfile != null)
        {
            _step = Step.Team;
        }

        // La partie a été lancée (par l'hôte) : on ferme le menu
        if (_step == Step.Waiting && GameState.Instance != null && GameState.Instance.IsStarted)
        {
            _step = Step.Done;
        }

        IsOpen = _step != Step.Done;

        if (IsOpen)
        {
            // Curseur libre pendant le menu : PlayerInput fige le joueur
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            _cursorRestored = false;
        }
        else if (_cursorRestored == false)
        {
            _cursorRestored = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void RefreshReferences()
    {
        if (_runner == null || _runner.IsRunning == false)
        {
            _runner = null;
            var enumerator = NetworkRunner.GetInstancesEnumerator();
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

        _profiles = FindObjectsByType<PlayerProfile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        _localProfile = null;
        foreach (var profile in _profiles)
        {
            if (profile.Object != null && profile.Object.IsValid && profile.HasInputAuthority)
            {
                _localProfile = profile;
                break;
            }
        }
    }

    private void OnGUI()
    {
        if (IsOpen == false)
            return;

        UITheme.Begin();
        EnsureStyles();

        // Voile sombre sur le jeu (spec : #080502 @ 60 %)
        GUI.color = UITheme.WithAlpha(UITheme.LobbyDim, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, UITheme.VirtualWidth, UITheme.VirtualHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        switch (_step)
        {
            case Step.Nickname:
                DrawNicknameStep();
                break;
            case Step.Connecting:
                DrawConnectingStep();
                break;
            case Step.Team:
                DrawTeamStep();
                break;
            case Step.Waiting:
                DrawWaitingStep();
                break;
        }
    }

    // ÉCRAN 1 — PSEUDO

    private void DrawNicknameStep()
    {
        float centerX = UITheme.VirtualWidth * 0.5f;
        var panel = new Rect(centerX - 330f, 210f, 660f, 640f);
        DrawLobbyPanel(panel);

        GUI.Label(new Rect(panel.x, panel.y + 46f, panel.width, 100f), "TidyAddict", _logoStyle);
        GUI.Label(new Rect(panel.x, panel.y + 152f, panel.width, 28f),
            "Gobelins, grimoires et grand bazar.", _subtitleStyle);

        GUI.Label(new Rect(panel.x + 70f, panel.y + 216f, panel.width - 140f, 24f), "TON NOM DE GOBELIN", _labelCapsStyle);

        var fieldRect = new Rect(panel.x + 70f, panel.y + 244f, panel.width - 140f, 62f);
        UITheme.DrawRounded(fieldRect, UITheme.WithAlpha(Color.black, 0.4f), 10f);
        UITheme.DrawBorder(fieldRect, UITheme.WithAlpha(UITheme.Brass, 0.55f), 1.5f, 10f);
        GUI.SetNextControlName("NicknameField");
        _nickname = GUI.TextField(new Rect(fieldRect.x + 20f, fieldRect.y, fieldRect.width - 40f, fieldRect.height), _nickname, 12, _fieldStyle);

        GUI.Label(new Rect(panel.x + 70f, panel.y + 326f, panel.width - 140f, 24f), "NOM DE LA SALLE", _labelCapsStyle);

        var roomRect = new Rect(panel.x + 70f, panel.y + 354f, panel.width - 140f, 62f);
        UITheme.DrawRounded(roomRect, UITheme.WithAlpha(Color.black, 0.4f), 10f);
        UITheme.DrawBorder(roomRect, UITheme.WithAlpha(UITheme.Brass, 0.55f), 1.5f, 10f);
        _room = GUI.TextField(new Rect(roomRect.x + 20f, roomRect.y, roomRect.width - 40f, roomRect.height), _room, 24, _fieldStyle);

        bool valid = string.IsNullOrWhiteSpace(_nickname) == false && _nickname.Trim().Length >= 3 &&
                     string.IsNullOrWhiteSpace(_room) == false;
        bool submit = PrimaryButton(new Rect(panel.x + 70f, panel.y + 452f, panel.width - 140f, 64f), "REJOINDRE LA SALLE", valid);

        if (valid && Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            submit = true;
        }

        GUI.Label(new Rect(panel.x, panel.y + 536f, panel.width, 24f),
            "Pseudo : 3 à 12 caractères  •  Même salle = même partie  •  Entrée pour valider", _subtitleStyle);

        if (submit)
        {
            _nickname = _nickname.Trim();
            _room = _room.Trim();
            StartConnection();
        }
    }

    // Démarre Fusion (AutoHostOrClient) sur la salle choisie — remplace le
    // démarrage automatique et l'UI de debug FusionBootstrapDebugGUI
    private void StartConnection()
    {
        // Déjà connecté (scène pas encore migrée en StartMode Manual) : on continue
        if (_runner != null && _runner.IsRunning)
        {
            _step = Step.Team;
            return;
        }

        var bootstrap = FindFirstObjectByType<Fusion.FusionBootstrap>(FindObjectsInactive.Include);
        if (bootstrap == null)
        {
            Debug.LogError("[LobbyMenu] FusionBootstrap introuvable dans la scène.");
            return;
        }

        bootstrap.DefaultRoomName = _room;
        bootstrap.StartAutoClient();
        _step = Step.Connecting;
    }

    private void DrawConnectingStep()
    {
        float centerX = UITheme.VirtualWidth * 0.5f;
        var panel = new Rect(centerX - 330f, 420f, 660f, 220f);
        DrawLobbyPanel(panel);

        int dots = 1 + (int)(Time.unscaledTime * 2f) % 3;
        GUI.Label(new Rect(panel.x, panel.y + 60f, panel.width, 60f),
            $"Connexion{new string('.', dots)}", _titleStyle);
        GUI.Label(new Rect(panel.x, panel.y + 136f, panel.width, 28f),
            $"Salle « <b>{_room}</b> »", _subtitleStyle);
    }

    // ÉCRAN 2 — CHOIX DU CLAN

    private void DrawTeamStep()
    {
        float centerX = UITheme.VirtualWidth * 0.5f;

        CountTeams(out int redCount, out int blueCount);
        int maxPerTeam = GameState.Instance != null ? GameState.Instance.MaxPlayersPerTeam : 2;
        bool redFull = redCount >= maxPerTeam;
        bool blueFull = blueCount >= maxPerTeam;
        bool bothFull = redFull && blueFull;

        float panelHeight = bothFull ? 720f : 640f;
        var panel = new Rect(centerX - 520f, 540f - panelHeight * 0.5f, 1040f, panelHeight);
        DrawLobbyPanel(panel);

        GUI.Label(new Rect(panel.x, panel.y + 42f, panel.width, 80f), "Choisis ton clan", _titleStyle);

        float cardsY = panel.y + 160f;
        DrawTeamCard(new Rect(centerX - 460f, cardsY, 440f, 360f), Team.Red, "ROUGE", redCount, maxPerTeam, redFull);
        DrawTeamCard(new Rect(centerX + 20f, cardsY, 440f, 360f), Team.Blue, "BLEU", blueCount, maxPerTeam, blueFull);

        GUI.Label(new Rect(panel.x, cardsY + 380f, panel.width, 26f),
            $"Connecté en tant que <b>{_nickname}</b>", _subtitleStyle);

        if (bothFull)
        {
            if (GhostButton(new Rect(centerX - 260f, cardsY + 420f, 520f, 64f), "REGARDER EN SPECTATEUR (FANTÔME)"))
                _wantSpectator = true;
        }
    }

    private void DrawTeamCard(Rect rect, Team team, string label, int count, int max, bool full)
    {
        Color teamColor = PlayerProfile.ColorOfTeam(team);
        Color darkText = team == Team.Red ? UITheme.TeamRedDarkText : UITheme.TeamBlueDarkText;

        if (full)
        {
            // Spec : pleine → fond 14 %, bord 30 %, désaturé
            UITheme.DrawRounded(rect, UITheme.WithAlpha(teamColor, 0.14f), 20f);
            UITheme.DrawBorder(rect, UITheme.WithAlpha(teamColor, 0.3f), 1.5f, 20f);

            _cardTitleStyle.normal.textColor = UITheme.WithAlpha(teamColor, 0.45f);
            GUI.Label(new Rect(rect.x, rect.y + 90f, rect.width, 80f), label, _cardTitleStyle);
            _cardCountStyle.normal.textColor = UITheme.WithAlpha(UITheme.Parchment, 0.4f);
            GUI.Label(new Rect(rect.x, rect.y + 180f, rect.width, 40f), $"{count} / {max}", _cardCountStyle);
            _cardHintStyle.normal.textColor = UITheme.WithAlpha(UITheme.Parchment, 0.35f);
            GUI.Label(new Rect(rect.x, rect.y + 244f, rect.width, 26f), "COMPLET", _cardHintStyle);
            return;
        }

        bool hover = rect.Contains(Event.current.mousePosition);
        UITheme.DrawRounded(hover ? new Rect(rect.x, rect.y - 2f, rect.width, rect.height) : rect, teamColor, 20f);

        _cardTitleStyle.normal.textColor = darkText;
        GUI.Label(new Rect(rect.x, rect.y + 90f, rect.width, 80f), label, _cardTitleStyle);
        _cardCountStyle.normal.textColor = darkText;
        GUI.Label(new Rect(rect.x, rect.y + 180f, rect.width, 40f), $"{count} / {max}", _cardCountStyle);
        _cardHintStyle.normal.textColor = UITheme.WithAlpha(darkText, 0.7f);
        GUI.Label(new Rect(rect.x, rect.y + 244f, rect.width, 26f), "CLIQUE POUR REJOINDRE", _cardHintStyle);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            _team = (int)team;
            _step = Step.Waiting;
        }
    }

    // ÉCRAN 3 — SALLE D'ATTENTE

    private void DrawWaitingStep()
    {
        float centerX = UITheme.VirtualWidth * 0.5f;
        var gameState = GameState.Instance;
        int connected = gameState != null ? gameState.ConnectedPlayers : 0;
        int required = gameState != null ? gameState.RequiredPlayers : 4;
        bool isHost = _runner != null && _runner.IsServer;

        var panel = new Rect(centerX - 330f, 200f, 660f, 680f);
        DrawLobbyPanel(panel);

        GUI.Label(new Rect(panel.x, panel.y + 42f, panel.width, 80f), "Salle d'attente", _titleStyle);
        GUI.Label(new Rect(panel.x, panel.y + 130f, panel.width, 28f),
            $"Joueurs connectés : <b>{connected}/{required}</b>", _subtitleStyle);

        // Lignes joueurs (pseudos teintés) + emplacements vides
        float rowY = panel.y + 190f;
        int shown = 0;
        foreach (var profile in _profiles)
        {
            if (profile == null || profile.HasProfile == false || shown >= required)
                continue;

            var rowRect = new Rect(panel.x + 70f, rowY, panel.width - 140f, 52f);
            UITheme.DrawRounded(rowRect, UITheme.WithAlpha(Color.black, 0.35f), 10f);

            // Carré d'équipe 11 px + pseudo teinté
            UITheme.DrawRounded(new Rect(rowRect.x + 18f, rowRect.y + 20f, 12f, 12f), PlayerProfile.ColorOfTeam(profile.Team), 3f);
            _bodyStyle.normal.textColor = UITheme.PseudoColor(profile.Team);
            GUI.Label(new Rect(rowRect.x + 44f, rowRect.y, rowRect.width - 60f, rowRect.height), profile.Nickname.ToString(), _bodyStyle);

            // Badge HÔTE sur notre ligne si on est le serveur
            if (isHost && profile == _localProfile)
            {
                var badge = new Rect(rowRect.xMax - 88f, rowRect.y + 12f, 70f, 28f);
                UITheme.DrawBorder(badge, UITheme.WithAlpha(UITheme.Gold, 0.7f), 1.5f, 8f);
                GUI.Label(badge, "HÔTE", _labelCapsGoldCentered ?? _labelCapsStyle);
            }

            rowY += 60f;
            shown++;
        }

        for (int i = shown; i < required; i++)
        {
            var rowRect = new Rect(panel.x + 70f, rowY, panel.width - 140f, 52f);
            UITheme.DrawBorder(rowRect, UITheme.WithAlpha(UITheme.Brass, 0.3f), 1.5f, 10f);
            _bodyStyle.normal.textColor = UITheme.WithAlpha(UITheme.TextDim, 0.6f);
            GUI.Label(new Rect(rowRect.x + 24f, rowRect.y, rowRect.width - 40f, rowRect.height), "En attente d'un gobelin…", _bodyStyle);
            rowY += 60f;
        }

        if (isHost && gameState != null)
        {
            bool canStart = gameState.CanStart;
            string label = canStart ? "LANCER LA PARTIE" : $"LANCER LA PARTIE — {required} JOUEURS REQUIS";
            if (PrimaryButton(new Rect(panel.x + 70f, panel.yMax - 160f, panel.width - 140f, 64f), label, canStart))
                gameState.StartGame();

            if (DebugButton(new Rect(panel.x + 70f, panel.yMax - 82f, panel.width - 140f, 44f), "⚑  LANCER MAINTENANT (DEBUG)"))
                gameState.StartGame();
        }
        else
        {
            GUI.Label(new Rect(panel.x, panel.yMax - 120f, panel.width, 28f),
                "En attente du lancement par l'hôte…", _subtitleStyle);
        }
    }

    // COMPOSANTS

    private void DrawLobbyPanel(Rect rect)
    {
        UITheme.DrawRounded(rect, UITheme.WithAlpha(UITheme.Panel, 0.92f), 18f);
        UITheme.DrawBorder(rect, UITheme.WithAlpha(UITheme.Brass, 0.45f), 1.5f, 18f);
    }

    private bool PrimaryButton(Rect rect, string label, bool enabled)
    {
        bool hover = enabled && rect.Contains(Event.current.mousePosition);
        var drawRect = hover ? new Rect(rect.x, rect.y - 2f, rect.width, rect.height) : rect;

        if (enabled)
        {
            UITheme.DrawRounded(drawRect, UITheme.Parchment, 12f);
            _buttonTextStyle.normal.textColor = UITheme.Hex("#221708");
        }
        else
        {
            UITheme.DrawRounded(drawRect, UITheme.WithAlpha(UITheme.Parchment, 0.08f), 12f);
            _buttonTextStyle.normal.textColor = UITheme.WithAlpha(UITheme.Parchment, 0.35f);
        }

        GUI.Label(drawRect, label, _buttonTextStyle);
        return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private bool GhostButton(Rect rect, string label)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        var drawRect = hover ? new Rect(rect.x, rect.y - 2f, rect.width, rect.height) : rect;

        UITheme.DrawRounded(drawRect, UITheme.WithAlpha(UITheme.Hex("#EAF4FF"), 0.08f), 12f);
        UITheme.DrawBorder(drawRect, UITheme.WithAlpha(UITheme.Hex("#EAF4FF"), 0.8f), 1.5f, 12f);
        _buttonTextStyle.normal.textColor = UITheme.Hex("#EAF4FF");
        GUI.Label(drawRect, label, _buttonTextStyle);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private bool DebugButton(Rect rect, string label)
    {
        UITheme.DrawBorder(rect, UITheme.WithAlpha(UITheme.Hex("#FFEE4D"), 0.5f), 1.5f, 10f);
        _labelCapsStyle.normal.textColor = UITheme.WithAlpha(UITheme.Hex("#FFEE4D"), 0.85f);
        var centered = new GUIStyle(_labelCapsStyle) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(rect, label, centered);
        _labelCapsStyle.normal.textColor = UITheme.TextDim;
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private void CountTeams(out int redCount, out int blueCount)
    {
        redCount = 0;
        blueCount = 0;
        foreach (var profile in _profiles)
        {
            if (profile == null || profile.HasProfile == false)
                continue;
            if (profile.Team == Team.Red)
                redCount++;
            else if (profile.Team == Team.Blue)
                blueCount++;
        }
    }

    private GUIStyle _labelCapsGoldCentered;

    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _logoStyle = UITheme.Label(UITheme.Display, 84, UITheme.Parchment, TextAnchor.MiddleCenter);
        _titleStyle = UITheme.Label(UITheme.Display, 64, UITheme.Parchment, TextAnchor.MiddleCenter);
        _subtitleStyle = UITheme.Label(UITheme.Body, 20, UITheme.TextDim, TextAnchor.MiddleCenter);
        _labelCapsStyle = UITheme.Label(UITheme.BodyExtraBold, 15, UITheme.TextDim, TextAnchor.MiddleLeft);
        _bodyStyle = UITheme.Label(UITheme.BodyBold, 20, UITheme.Parchment, TextAnchor.MiddleLeft);
        _buttonTextStyle = UITheme.Label(UITheme.BodyExtraBold, 20, UITheme.Parchment, TextAnchor.MiddleCenter);
        _cardTitleStyle = UITheme.Label(UITheme.Display, 62, Color.white, TextAnchor.MiddleCenter);
        _cardCountStyle = UITheme.Label(UITheme.BodyBold, 30, Color.white, TextAnchor.MiddleCenter);
        _cardHintStyle = UITheme.Label(UITheme.BodyExtraBold, 15, Color.white, TextAnchor.MiddleCenter);
        _labelCapsGoldCentered = UITheme.Label(UITheme.BodyExtraBold, 13, UITheme.Gold, TextAnchor.MiddleCenter);

        _fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            font = UITheme.Body,
            fontSize = 26,
            alignment = TextAnchor.MiddleCenter,
        };
        _fieldStyle.normal.textColor = UITheme.Parchment;
        _fieldStyle.focused.textColor = UITheme.Parchment;
        _fieldStyle.normal.background = null;
        _fieldStyle.focused.background = null;
    }
}
