using System.Text;

/// <summary>
/// Table des mots de sort et grammaire fermée pour Vosk : le moteur ne peut
/// reconnaître QUE ces mots (+ [unk] pour le bruit). Contrainte Vosk : les mots
/// doivent exister dans le vocabulaire du modèle français, d'où des mots
/// français courants plutôt que du pseudo-latin.
/// </summary>
public static class SpellWords
{
    public static readonly string[] Words =
    {
        "flamme", "éclair", "tempête", "glace", "ombre",
        "lumière", "pierre", "tonnerre", "étoile", "lune",
        "foudre", "sang", "esprit", "brume", "cendre",
        "givre", "orage", "poison", "racine", "cristal",
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
}
