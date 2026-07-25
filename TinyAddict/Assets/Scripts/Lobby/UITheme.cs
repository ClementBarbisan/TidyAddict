using UnityEngine;

/// <summary>
/// Design system « dark fantasy médiéval » de TidyAddict (spec Claude Design) :
/// couleurs, polices (Grenze Gotisch display / Alegreya Sans corps) et helpers
/// de dessin IMGUI (panneaux arrondis à bord laiton, jauges, pills).
/// Toutes les UI du jeu passent par ici pour rester cohérentes.
/// L'échelle est calée sur une référence 1080p via GUI.matrix.
/// </summary>
public static class UITheme
{
    // NEUTRES
    public static readonly Color Night = Hex("#14100C");
    public static readonly Color Panel = Hex("#1A130C");
    public static readonly Color PanelHud = Hex("#140E08");
    public static readonly Color Brass = Hex("#94743C");
    public static readonly Color Parchment = Hex("#EFE2C0");
    public static readonly Color TextDim = Hex("#A6906B");
    public static readonly Color Gold = Hex("#C9A227");
    public static readonly Color LobbyDim = Hex("#080502");

    // ÉQUIPES
    public static readonly Color TeamRed = Hex("#FF4D40");
    public static readonly Color TeamBlue = Hex("#4D8CFF");
    public static readonly Color TeamRedDarkText = Hex("#1A0705");
    public static readonly Color TeamBlueDarkText = Hex("#050D1F");
    public static readonly Color PseudoRed = Hex("#FF8F84");   // pseudo rouge sur fond sombre
    public static readonly Color PseudoBlue = Hex("#8FB5FF");  // pseudo bleu sur fond sombre

    // SYSTÈME
    public static readonly Color Danger = Hex("#FF5A5A");
    public static readonly Color Urgency = Hex("#FF9E2C");
    public static readonly Color Success = Hex("#6BFFB8");

    public static string PseudoHex(Team team)
    {
        return team == Team.Red ? "FF8F84" : team == Team.Blue ? "8FB5FF" : "EFE2C0";
    }

    public static Color PseudoColor(Team team)
    {
        return team == Team.Red ? PseudoRed : team == Team.Blue ? PseudoBlue : Parchment;
    }

    // POLICES (chargées depuis Resources/Fonts, fallback police système)
    private static Font _display;
    private static Font _body;
    private static Font _bodyBold;
    private static Font _bodyExtraBold;

    public static Font Display => _display != null ? _display : LoadFonts()._display2;
    public static Font Body => _body != null ? _body : LoadFonts()._body2;
    public static Font BodyBold => _bodyBold != null ? _bodyBold : LoadFonts()._bodyBold2;
    public static Font BodyExtraBold => _bodyExtraBold != null ? _bodyExtraBold : LoadFonts()._bodyExtraBold2;

    private static (Font _display2, Font _body2, Font _bodyBold2, Font _bodyExtraBold2) LoadFonts()
    {
        _display = Resources.Load<Font>("Fonts/GrenzeGotisch");
        _body = Resources.Load<Font>("Fonts/AlegreyaSans");
        _bodyBold = Resources.Load<Font>("Fonts/AlegreyaSans-Bold");
        _bodyExtraBold = Resources.Load<Font>("Fonts/AlegreyaSans-ExtraBold");

        var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_display == null) _display = fallback;
        if (_body == null) _body = fallback;
        if (_bodyBold == null) _bodyBold = _body;
        if (_bodyExtraBold == null) _bodyExtraBold = _bodyBold;

        return (_display, _body, _bodyBold, _bodyExtraBold);
    }

    // ÉCHELLE 1080p : toutes les UI dessinent en coordonnées 1920×1080
    public static float Scale => Mathf.Max(0.5f, Screen.height / 1080f);
    public static float VirtualWidth => Screen.width / Scale;
    public static float VirtualHeight => 1080f;

    /// <summary>À appeler en tête de chaque OnGUI : cale le dessin sur la référence 1080p.</summary>
    public static void Begin()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
    }

    // DESSIN

    /// <summary>Panneau arrondi avec bord laiton (spec : fond @85–92 %, bord 1.5 px @55 %).</summary>
    public static void DrawPanel(Rect rect, float radius = 12f, float fillAlpha = 0.88f)
    {
        DrawRounded(rect, WithAlpha(PanelHud, fillAlpha), radius);
        DrawBorder(rect, WithAlpha(Brass, 0.55f), 1.5f, radius);
    }

    public static void DrawRounded(Rect rect, Color color, float radius)
    {
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
    }

    public static void DrawBorder(Rect rect, Color color, float width, float radius)
    {
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
    }

    /// <summary>Jauge (spec : piste parchemin 10 %, remplissage couleur d'équipe, r selon hauteur).</summary>
    public static void DrawGauge(Rect rect, float fill01, Color fillColor)
    {
        float radius = rect.height * 0.5f;
        DrawRounded(rect, WithAlpha(Parchment, 0.1f), radius);

        float width = Mathf.Max(rect.height, rect.width * Mathf.Clamp01(fill01));
        if (fill01 > 0.001f)
            DrawRounded(new Rect(rect.x, rect.y, width, rect.height), fillColor, radius);
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }

    // STYLES

    public static GUIStyle Label(Font font, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool richText = true)
    {
        return new GUIStyle(GUIStyle.none)
        {
            font = font,
            fontSize = size,
            alignment = anchor,
            richText = richText,
            normal = { textColor = color },
            wordWrap = false,
        };
    }
}
