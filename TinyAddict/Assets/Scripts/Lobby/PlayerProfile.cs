using Fusion;
using UnityEngine;

/// <summary>
/// Profil réseau du joueur : pseudo + équipe (1 = rouge, 2 = bleu), choisis
/// dans le menu de lobby. Affiche un pseudo flottant coloré au-dessus de la
/// tête, visible par les autres joueurs (masqué si le joueur est invisible).
/// </summary>
public class PlayerProfile : NetworkBehaviour
{
    public static readonly Color RedTeamColor = new Color(1f, 0.3f, 0.25f);
    public static readonly Color BlueTeamColor = new Color(0.3f, 0.55f, 1f);

    [SerializeField] private float _nameTagHeight = 2.2f;

    [Networked] public NetworkString<_16> Nickname { get; set; }
    [Networked] public int Team { get; set; }

    private TextMesh _nameTag;
    private PlayerSpellEffects _effects;

    public bool HasProfile => Object != null && Object.IsValid && Team != 0;

    public Color TeamColor => Team == 1 ? RedTeamColor : Team == 2 ? BlueTeamColor : Color.white;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetProfile(string nickname, int team)
    {
        Nickname = nickname;
        Team = Mathf.Clamp(team, 1, 2);
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
        bool show = HasInputAuthority == false && Team != 0 &&
                    (_effects == null || _effects.IsInvisible == false);

        if (_nameTag.gameObject.activeSelf != show)
            _nameTag.gameObject.SetActive(show);

        if (show == false)
            return;

        string nickname = Nickname.ToString();
        if (_nameTag.text != nickname)
            _nameTag.text = nickname;
        _nameTag.color = TeamColor;

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
