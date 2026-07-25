using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Table des mots de sort (pseudo-latin) et comparaison floue entre
/// ce que Whisper a entendu et le mot attendu.
/// </summary>
public static class SpellWords
{
    public static readonly string[] Words =
    {
        "ignis", "aqua", "ventus", "terra", "fulgur",
        "glacies", "umbra", "lux", "flamma", "petra",
        "tempestas", "tonitrus", "stella", "luna", "ferrum",
        "sanguis", "spiritus", "vita", "mortis", "aurum",
    };

    /// <summary>Prompt donné à Whisper pour biaiser la transcription vers nos mots.</summary>
    public static string InitialPrompt =>
        "Le joueur prononce un seul mot magique en latin parmi cette liste : " +
        string.Join(", ", Words) + ". Transcris uniquement ce mot.";

    /// <summary>
    /// Vocabulaire fermé : trouve le mot de sort le plus proche de la transcription.
    /// Retourne l'index dans <see cref="Words"/> et l'erreur correspondante.
    /// </summary>
    public static int FindClosest(string heard, out float bestError)
    {
        int bestIndex = -1;
        bestError = 1f;

        for (int i = 0; i < Words.Length; i++)
        {
            float error = MatchError(heard, Words[i]);
            if (error < bestError)
            {
                bestError = error;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Erreur de correspondance entre 0 (parfait) et 1 (rien à voir).
    /// Compare la phrase entière ET chaque mot entendu séparément, garde le meilleur.
    /// </summary>
    public static float MatchError(string heard, string expected)
    {
        string target = Normalize(expected);
        string full = Normalize(heard);

        if (full.Length == 0 || target.Length == 0)
            return 1f;

        float best = ErrorRatio(full, target);

        foreach (string token in full.Split(' '))
        {
            if (token.Length == 0)
                continue;
            best = Math.Min(best, ErrorRatio(token, target));
        }

        return best;
    }

    /// <summary>Minuscules, accents retirés, seules les lettres et espaces conservés.</summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        bool lastWasSpace = true;

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetter(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (lastWasSpace == false)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        return sb.ToString().Trim();
    }

    private static float ErrorRatio(string a, string b)
    {
        int distance = Levenshtein(a, b);
        int maxLength = Math.Max(a.Length, b.Length);
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
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
