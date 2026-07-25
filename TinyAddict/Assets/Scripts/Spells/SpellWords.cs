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
}
