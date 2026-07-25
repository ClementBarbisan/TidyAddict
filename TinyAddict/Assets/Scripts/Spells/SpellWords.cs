using System.Text;

/// <summary>
/// Table des mots de sort et grammaire fermée pour Vosk : le moteur ne peut
/// reconnaître QUE ces mots (+ [unk] pour le bruit). Contrainte Vosk : chaque
/// mot doit exister dans le lexique du modèle français. Ces 20 mots latins ont
/// tous été vérifiés présents dans le vocabulaire de vosk-model-small-fr-0.22
/// (n'ajoutez pas de mot inventé : il serait ignoré par la reconnaissance).
/// </summary>
public static class SpellWords
{
    public static readonly string[] Words =
    {
        "aqua", "terra", "luna", "stella", "nova",
        "inferno", "petra", "anima", "draco", "fortuna",
        "gloria", "victoria", "sanctus", "dominus", "maximus",
        "solaris", "aurora", "corona", "omega", "vita",
    };

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
