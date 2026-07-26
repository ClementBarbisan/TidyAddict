using System.Text;
using UnityEngine;

public enum SpellType
{
    Ice,
    Fire,
    SpeedBuff,
    ForceBuff,
    Invisibility,
    Confusion,
    Shrink,
    Stun,
    Wall,
    BlackHole,
    Loot,
    Charge,
}

/// <summary>
/// Table des sorts : 5 mots latins, chaque mot déclenche TOUJOURS le même sort.
/// Contrainte Vosk : chaque mot doit exister dans le lexique du modèle français —
/// ces 5 mots ont été vérifiés présents dans vosk-model-small-fr-0.22
/// (n'ajoutez pas de mot inventé, il serait ignoré par la reconnaissance).
/// </summary>
public static class SpellWords
{
    public static readonly string[] Words =
    {
        "polaris",  // 0 - glace : ralentit les autres joueurs autour de soi
        "inferno",  // 1 - feu : boule de feu explosive
        "aurora",   // 2 - vitesse : buff de vitesse 30 s
        "maximus",  // 3 - force : grab/poussée renforcés 30 s
        "anima",    // 4 - invisibilité 30 s
        "vertigo",  // 5 - confusion : touches inversées de la cible 30 s
        "minima",   // 6 - réduction : la cible rapetisse et perd sa force 30 s
        "electra",  // 7 - électrification : la cible est paralysée 10 s
        "petra",    // 8 - mur de pierre devant soi pendant 30 s
        "pluto",    // 9 - trou noir : aspire joueurs et objets 4 s
        "fortuna",  // 10 - l'objet visé se téléporte dans notre zone de collecte
        "taurus",   // 11 - charge en avant qui percute tout
    };

    public static readonly SpellType[] Types =
    {
        SpellType.Ice,
        SpellType.Fire,
        SpellType.SpeedBuff,
        SpellType.ForceBuff,
        SpellType.Invisibility,
        SpellType.Confusion,
        SpellType.Shrink,
        SpellType.Stun,
        SpellType.Wall,
        SpellType.BlackHole,
        SpellType.Loot,
        SpellType.Charge,
    };

    // Couleurs officielles du design system (bord + lueur + ◆, jamais fond plein)
    public static readonly Color[] Colors =
    {
        new Color32(0x7E, 0xD6, 0xFF, 0xFF), // polaris - #7ED6FF
        new Color32(0xFF, 0x6B, 0x2C, 0xFF), // inferno - #FF6B2C
        new Color32(0xB3, 0x6B, 0xFF, 0xFF), // aurora  - #B36BFF
        new Color32(0xFF, 0xC9, 0x33, 0xFF), // maximus - #FFC933
        new Color32(0xEA, 0xF4, 0xFF, 0xFF), // anima   - #EAF4FF
        new Color32(0xFF, 0x6B, 0xD6, 0xFF), // vertigo - #FF6BD6
        new Color32(0x6B, 0xFF, 0xB8, 0xFF), // minima  - #6BFFB8
        new Color32(0xFF, 0xEE, 0x4D, 0xFF), // electra - #FFEE4D
        new Color32(0xAD, 0xB9, 0xC6, 0xFF), // petra   - #ADB9C6 gris pierre
        new Color32(0x6B, 0x5A, 0xE0, 0xFF), // pluto   - #6B5AE0 indigo profond
        new Color32(0x57, 0xD9, 0x41, 0xFF), // fortuna - #57D941 vert trèfle
        new Color32(0xC9, 0x79, 0x3B, 0xFF), // taurus  - #C9793B brun taureau
    };

    // Affiché sur le parchemin pour savoir ce que fait le sort avant de le lancer
    public static readonly string[] Descriptions =
    {
        "Freezes the ground: slows nearby players (not you)",
        "Explosive fireball: blasts everything on impact",
        "Speed boost for 30s",
        "You grow huge: stronger grab and push for 30s",
        "Invisibility for 30s",
        "Inverts the target's movement keys for 30s",
        "Shrinks the target: weaker push and grab for 30s",
        "Electrifies the target: paralyzed for 10s",
        "Raises a stone wall in front of you for 30s",
        "Black hole: pulls players and objects in for 4s",
        "Aim at an item: it teleports into your collection zone",
        "Charge forward: rams and knocks back everything",
    };

    public static SpellType TypeOf(int wordIndex)
    {
        return Types[Mathf.Abs(wordIndex) % Types.Length];
    }

    public static Color ColorOf(int wordIndex)
    {
        return Colors[Mathf.Abs(wordIndex) % Colors.Length];
    }

    public static string DescriptionOf(int wordIndex)
    {
        return Descriptions[Mathf.Abs(wordIndex) % Descriptions.Length];
    }

    /// <summary>Grammaire JSON passée à VoskRecognizer : vocabulaire fermé + [unk].</summary>
    public static string GrammarJson
    {
        get
        {
            var sb = new StringBuilder("[");
            foreach (string word in Words)
            {
                sb.Append('"').Append(word).Append("\", ");
            }
            sb.Append("\"[unk]\"]");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Erreur de correspondance entre 0 (identique) et 1 (rien à voir), calculée
    /// sur la distance de Levenshtein normalisée. Sert de marge d'erreur quand
    /// Vosk renvoie un mot voisin de celui attendu.
    /// </summary>
    public static float MatchError(string heard, string expected)
    {
        if (string.IsNullOrEmpty(heard) || string.IsNullOrEmpty(expected))
            return 1f;

        int distance = Levenshtein(heard, expected);
        int maxLength = System.Math.Max(heard.Length, expected.Length);
        return maxLength == 0 ? 1f : (float)distance / maxLength;
    }

    private static int Levenshtein(string a, string b)
    {
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = System.Math.Min(
                    System.Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
