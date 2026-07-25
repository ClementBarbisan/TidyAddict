using Fusion;
using UnityEngine;

/// <summary>
/// Menu de lobby affiché au lancement : choix du pseudo, puis de l'équipe
/// (rouge/bleu), puis attente des joueurs. L'hôte lance la partie quand
/// 4 joueurs sont connectés (ou en force avec le bouton debug).
/// Tant que le menu est ouvert, le curseur est libre : PlayerInput ignore
/// alors les entrées, le joueur est immobile. S'auto-instancie.
/// </summary>
public class LobbyMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private enum Step
    {
        Nickname,
        Team,
        Waiting,
        Done,
    }

    private const float RefreshInterval = 0.5f;

    private Step _step = Step.Nickname;
    private string _nickname = "";
    private int _team;
    private bool _profileSent;
    private bool _cursorRestored;
    private float _nextRefresh;

    private NetworkRunner _runner;
    private PlayerProfile[] _profiles = new PlayerProfile[0];
    private PlayerProfile _localProfile;

    private GUIStyle _titleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _fieldStyle;

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

        EnsureStyles();

        // Fond assombri
        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        var panel = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 160f, 440f, 320f);
        GUILayout.BeginArea(panel);
        GUILayout.BeginVertical();

        switch (_step)
        {
            case Step.Nickname:
                DrawNicknameStep();
                break;
            case Step.Team:
                DrawTeamStep();
                break;
            case Step.Waiting:
                DrawWaitingStep();
                break;
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawNicknameStep()
    {
        GUILayout.Label("Choisis ton pseudo", _titleStyle);
        GUILayout.Space(20f);

        GUI.SetNextControlName("NicknameField");
        _nickname = GUILayout.TextField(_nickname, 16, _fieldStyle, GUILayout.Height(44f));
        GUI.FocusControl("NicknameField");

        GUILayout.Space(20f);

        bool valid = string.IsNullOrWhiteSpace(_nickname) == false;
        GUI.enabled = valid;
        bool submit = GUILayout.Button("Valider", _buttonStyle, GUILayout.Height(48f));
        GUI.enabled = true;

        // Entrée valide aussi
        if (valid && Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            submit = true;
        }

        if (submit)
        {
            _nickname = _nickname.Trim();
            _step = Step.Team;
        }
    }

    private void DrawTeamStep()
    {
        GUILayout.Label("Choisis ton équipe", _titleStyle);
        GUILayout.Space(6f);
        GUILayout.Label(_nickname, _labelStyle);
        GUILayout.Space(16f);

        CountTeams(out int redCount, out int blueCount);

        GUILayout.BeginHorizontal();

        var previousColor = GUI.backgroundColor;
        GUI.backgroundColor = PlayerProfile.RedTeamColor;
        if (GUILayout.Button($"ROUGE\n({redCount} joueur{(redCount > 1 ? "s" : "")})", _buttonStyle, GUILayout.Height(90f)))
        {
            _team = 1;
            _step = Step.Waiting;
        }

        GUILayout.Space(12f);

        GUI.backgroundColor = PlayerProfile.BlueTeamColor;
        if (GUILayout.Button($"BLEU\n({blueCount} joueur{(blueCount > 1 ? "s" : "")})", _buttonStyle, GUILayout.Height(90f)))
        {
            _team = 2;
            _step = Step.Waiting;
        }

        GUI.backgroundColor = previousColor;
        GUILayout.EndHorizontal();
    }

    private void DrawWaitingStep()
    {
        var gameState = GameState.Instance;
        int connected = gameState != null ? gameState.ConnectedPlayers : 0;
        int required = gameState != null ? gameState.RequiredPlayers : 4;

        GUILayout.Label($"Joueurs connectés : {connected}/{required}", _titleStyle);
        GUILayout.Space(12f);

        // Liste des joueurs avec pseudo coloré par équipe
        foreach (var profile in _profiles)
        {
            if (profile == null || profile.HasProfile == false)
                continue;

            string hex = ColorUtility.ToHtmlStringRGB(profile.TeamColor);
            GUILayout.Label($"<color=#{hex}>{profile.Nickname}</color>", _labelStyle);
        }

        GUILayout.FlexibleSpace();

        bool isHost = _runner != null && _runner.IsServer;
        if (isHost && gameState != null)
        {
            GUI.enabled = gameState.CanStart;
            if (GUILayout.Button("Lancer la partie", _buttonStyle, GUILayout.Height(48f)))
                gameState.StartGame();
            GUI.enabled = true;

            GUILayout.Space(6f);
            if (GUILayout.Button("Lancer maintenant (debug)", _buttonStyle, GUILayout.Height(34f)))
                gameState.StartGame();
        }
        else
        {
            GUILayout.Label("En attente du lancement par l'hôte...", _labelStyle);
        }
    }

    private void CountTeams(out int redCount, out int blueCount)
    {
        redCount = 0;
        blueCount = 0;
        foreach (var profile in _profiles)
        {
            if (profile == null || profile.HasProfile == false)
                continue;
            if (profile.Team == 1)
                redCount++;
            else if (profile.Team == 2)
                blueCount++;
        }
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            richText = true,
            alignment = TextAnchor.MiddleCenter,
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
        };

        _fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter,
        };
    }
}
