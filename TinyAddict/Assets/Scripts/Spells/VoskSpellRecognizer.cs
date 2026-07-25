using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Vosk;

/// <summary>
/// Service de reconnaissance des mots de sort basé sur Vosk (offline,
/// Windows/macOS/Linux). Le modèle français est chargé une seule fois en
/// tâche de fond ; chaque reconnaissance utilise la grammaire fermée de
/// <see cref="SpellWords"/> : seul un des 20 mots (ou [unk]) peut sortir.
/// </summary>
public static class VoskSpellRecognizer
{
    private const string ModelFolder = "VoskModel/vosk-model-small-fr-0.22";

    private static Model _model;
    private static Task<Model> _loadTask;
    private static readonly object _gate = new object();

    public static bool IsReady => _model != null;

    /// <summary>À appeler tôt (spawn du joueur) pour éviter la latence au premier sort.</summary>
    public static void Preload()
    {
        EnsureModelAsync();
    }

    /// <summary>
    /// Reconnaît un mot de sort dans des échantillons mono (-1..1).
    /// Retourne le mot reconnu, ou null si rien d'exploitable.
    /// </summary>
    public static async Task<string> RecognizeAsync(float[] samples, int sampleRate)
    {
        Model model;
        try
        {
            model = await EnsureModelAsync();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[VoskSpellRecognizer] Échec du chargement du modèle : {exception.Message}");
            return null;
        }

        string grammar = SpellWords.GrammarJson;

        string json = await Task.Run(() =>
        {
            // Kaldi attend des échantillons à l'échelle 16 bits
            var pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                pcm[i] = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            }

            using (var recognizer = new VoskRecognizer(model, sampleRate, grammar))
            {
                recognizer.AcceptWaveform(pcm, pcm.Length);
                return recognizer.FinalResult();
            }
        });

        return ExtractText(json);
    }

    private static Task<Model> EnsureModelAsync()
    {
        lock (_gate)
        {
            if (_loadTask == null)
            {
                string modelPath = Path.Combine(Application.streamingAssetsPath, ModelFolder);
                if (Directory.Exists(modelPath) == false)
                {
                    _loadTask = Task.FromException<Model>(
                        new DirectoryNotFoundException($"Modèle Vosk introuvable : {modelPath}"));
                    return _loadTask;
                }

                Vosk.Vosk.SetLogLevel(-1);
                _loadTask = Task.Run(() =>
                {
                    var model = new Model(modelPath);
                    _model = model;
                    return model;
                });
            }

            return _loadTask;
        }
    }

    // Extrait la valeur de "text" du JSON {"text" : "flamme"} sans dépendance
    private static string ExtractText(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        const string key = "\"text\"";
        int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0)
            return null;

        int firstQuote = json.IndexOf('"', keyIndex + key.Length);
        if (firstQuote < 0)
            return null;

        int lastQuote = json.IndexOf('"', firstQuote + 1);
        if (lastQuote < 0)
            return null;

        string text = json.Substring(firstQuote + 1, lastQuote - firstQuote - 1).Trim();
        return text.Length == 0 ? null : text;
    }
}
