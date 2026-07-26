using Fusion;
using UnityEngine;

/// <summary>
/// Profil réseau du joueur : pseudo (networked) + équipe, gérée par le
/// TeamManager (source de vérité unique — le choix du lobby et les zones
/// TriggerPlayerDetector passent tous les deux par lui). La couleur du corps
/// passe par NetworkedColor. Affiche un pseudo flottant coloré au-dessus de
/// la tête, masqué si le joueur est invisible.
/// </summary>
public class PlayerProfile : NetworkBehaviour
{
    // Couleurs officielles du design system (#FF4D40 / #4D8CFF)
    public static readonly Color RedTeamColor = new Color32(0xFF, 0x4D, 0x40, 0xFF);
    public static readonly Color BlueTeamColor = new Color32(0x4D, 0x8C, 0xFF, 0xFF);

    public static Color ColorOfTeam(Team team)
    {
        return team == Team.Red ? RedTeamColor : team == Team.Blue ? BlueTeamColor : Color.white;
    }

    [SerializeField] private float _nameTagHeight = 2.2f;

    [Networked] public NetworkString<_16> Nickname { get; set; }

    private TextMesh _nameTag;
    private PlayerSpellEffects _effects;

    public Team Team
    {
        get
        {
            if (Object == null || Object.IsValid == false)
                return Team.None;

            var teamManager = TeamManager.Instance;
            // Le dictionnaire d'équipes n'est lisible qu'une fois le TeamManager spawné
            if (teamManager == null || teamManager.Object == null || teamManager.Object.IsValid == false)
                return Team.None;

            return teamManager.GetPlayerTeam(Object.InputAuthority);
        }
    }

    public bool HasProfile => Team != Team.None;
    public Color TeamColor => ColorOfTeam(Team);

    public override void Spawned()
    {
        // Corps neutre tant qu'aucune équipe n'est choisie (le Color networked
        // par défaut est noir transparent, pas ce qu'on veut voir)
        if (Object.HasStateAuthority)
        {
            var color = GetComponent<NetworkedColor>();
            if (color != null && color.ObjectColor == default)
                color.ObjectColor = Color.white;
        }
    }

    /// <summary>Téléporte le joueur (serveur) et réaligne sa caméra (client propriétaire).</summary>
    public void TeleportTo(Vector3 position, float yaw)
    {
        var kcc = GetComponent<Fusion.Addons.SimpleKCC.SimpleKCC>();
        if (kcc != null)
        {
            kcc.SetPosition(position);
            kcc.SetLookRotation(0f, yaw);
        }
        else
        {
            transform.position = position;
        }

        RPC_ResetLook(yaw);
    }

    // La rotation de regard est accumulée côté client (absolue) : sans ce reset,
    // le client écraserait l'orientation de spawn au premier mouvement de souris
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ResetLook(float yaw)
    {
        var playerInput = GetComponent<Projectiles.PlayerInput>();
        if (playerInput != null)
            playerInput.SetLookRotation(new Vector2(0f, yaw));
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_BecomeSpectator()
    {
        // Équipes pleines : le serveur retire le personnage — plus de corps,
        // plus d'interactions possibles, le client passe en caméra libre
        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetProfile(string nickname, int team)
    {
        Nickname = nickname;
        var chosenTeam = (Team)Mathf.Clamp(team, (int)Team.Red, (int)Team.Blue);

        if (TeamManager.Instance != null)
            TeamManager.Instance.SetPlayerTeam(Object.InputAuthority, chosenTeam);

        var color = GetComponent<NetworkedColor>();
        if (color != null)
            color.RequestColorChange(ColorOfTeam(chosenTeam));
    }

    private void Awake()
    {
        _effects = GetComponent<PlayerSpellEffects>();
    }

    private void LateUpdate()
    {
        if (Object == null || Object.IsValid == false)
            return;

        if (_nameTag == null)
            CreateNameTag();

        // Pas de tag au-dessus de sa propre tête, ni sur un profil vide,
        // ni sur un joueur invisible (ça trahirait le sort anima)
        Team team = Team;
        bool show = HasInputAuthority == false && team != Team.None &&
                    (_effects == null || _effects.IsInvisible == false);

        if (_nameTag.gameObject.activeSelf != show)
            _nameTag.gameObject.SetActive(show);

        if (show == false)
            return;

        string nickname = Nickname.ToString();
        if (_nameTag.text != nickname)
            _nameTag.text = nickname;
        // Teinte lisible sur fond sombre (spec design : #FF8F84 / #8FB5FF)
        _nameTag.color = UITheme.PseudoColor(team);

        // Toujours face à la caméra
        if (Camera.main != null)
        {
            _nameTag.transform.rotation = Quaternion.LookRotation(
                _nameTag.transform.position - Camera.main.transform.position);
        }
    }

    private void CreateNameTag()
    {
        var tagObject = new GameObject("NameTag");
        tagObject.transform.SetParent(transform, false);
        tagObject.transform.localPosition = new Vector3(0f, _nameTagHeight, 0f);

        _nameTag = tagObject.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _nameTag.font = font;
        _nameTag.fontSize = 48;
        _nameTag.characterSize = 0.05f;
        _nameTag.anchor = TextAnchor.MiddleCenter;
        _nameTag.alignment = TextAlignment.Center;
        _nameTag.fontStyle = FontStyle.Bold;
        tagObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        tagObject.SetActive(false);
    }
}
