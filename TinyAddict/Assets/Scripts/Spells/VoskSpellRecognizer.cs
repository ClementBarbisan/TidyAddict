using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Vosk;

/// <summary>
/// Service de reconnaissance des mots de sort basé sur Vosk (offline,
/// Windows/macOS/Linux). Le modèle français est chargé une seule fois en
/// tâche de fond ; chaque reconnaissance utilise la grammaire fermée de
/// <see cref="SpellWords"/> et renvoie jusqu'à 3 hypothèses.
/// </summary>
public static class VoskSpellRecognizer
{
    private const string ModelFolder = "VoskModel/vosk-model-small-fr-0.22";
    private const int MaxAlternatives = 3;

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
    /// Retourne les hypothèses de Vosk (souvent 1 à 3), liste vide si rien.
    /// </summary>
    public static async Task<List<string>> RecognizeAsync(float[] samples, int sampleRate)
    {
        Model model;
        try
        {
            model = await EnsureModelAsync();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Vosk] Échec du chargement du modèle : {exception.Message}");
            return new List<string>();
        }

        string grammar = SpellWords.GrammarJson;

        string json = await Task.Run(() =>
        {
            // Kaldi attend des échantillons à l'échelle 16 bits
            float peak = 0f;
            var pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float sampleAbs = samples[i] < 0f ? -samples[i] : samples[i];
                if (sampleAbs > peak)
                    peak = sampleAbs;
                pcm[i] = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            }

            string result;
            using (var recognizer = new VoskRecognizer(model, sampleRate, grammar))
            {
                recognizer.SetMaxAlternatives(MaxAlternatives);
                recognizer.AcceptWaveform(pcm, pcm.Length);
                result = recognizer.FinalResult();
            }

            float duration = sampleRate > 0 ? (float)samples.Length / sampleRate : 0f;
            Debug.Log($"[Vosk] audio {duration:F2}s @ {sampleRate}Hz, niveau max {peak:F3} " +
                      $"{(peak < 0.02f ? "(⚠ quasi silencieux — mauvais micro capté ?)" : "")}\n[Vosk] réponse brute : {result}");

            return result;
        });

        return ExtractTexts(json);
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

    // Extrait toutes les valeurs de "text" du JSON, que la réponse soit
    // {"text" : "terra"} ou {"alternatives" : [{"text" : "terra", ...}, ...]}
    private static List<string> ExtractTexts(string json)
    {
        var texts = new List<string>();
        if (string.IsNullOrEmpty(json))
            return texts;

        const string key = "\"text\"";
        int searchIndex = 0;

        while (true)
        {
            int keyIndex = json.IndexOf(key, searchIndex, StringComparison.Ordinal);
            if (keyIndex < 0)
                break;

            int firstQuote = json.IndexOf('"', keyIndex + key.Length);
            if (firstQuote < 0)
                break;

            int lastQuote = json.IndexOf('"', firstQuote + 1);
            if (lastQuote < 0)
                break;

            string text = json.Substring(firstQuote + 1, lastQuote - firstQuote - 1).Trim();
            if (text.Length > 0)
                texts.Add(text);

            searchIndex = lastQuote + 1;
        }

        return texts;
    }
}
