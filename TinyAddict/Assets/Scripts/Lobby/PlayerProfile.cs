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
    public static readonly Color RedTeamColor = new Color(1f, 0.3f, 0.25f);
    public static readonly Color BlueTeamColor = new Color(0.3f, 0.55f, 1f);

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
            if (Object == null || Object.IsValid == false || TeamManager.Instance == null)
                return Team.None;
            return TeamManager.Instance.GetPlayerTeam(Object.InputAuthority);
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
        _nameTag.color = ColorOfTeam(team);

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
