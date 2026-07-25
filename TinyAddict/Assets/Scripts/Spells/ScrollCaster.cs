using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using Whisper;

/// <summary>
/// Côté joueur : porte le parchemin, gère l'incantation (clic gauche maintenu =
/// enregistrement du micro Photon, relâché = transcription Whisper + comparaison
/// floue avec le mot du parchemin) et demande au serveur de lancer le sort.
/// G pour reposer le parchemin. Pendant qu'on tient un parchemin, les autres
/// interactions des mains (tir, grab) sont bloquées.
/// </summary>
public class ScrollCaster : NetworkBehaviour
{
    [SerializeField] private Transform _handAnchor;
    [SerializeField] private Transform _castOrigin;
    [SerializeField] private NetworkObject _spellBallPrefab;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Erreur maximale tolérée entre le mot dit et le mot attendu (0 = parfait, 1 = tout accepté)")]
    private float _errorThreshold = 0.4f;
    [SerializeField] private float _minRecordSeconds = 0.35f;
    [SerializeField] private float _maxRecordSeconds = 6f;

    [Networked] public SpellScroll HeldScroll { get; set; }

    public bool IsHoldingScroll => Object != null && Object.IsValid && HeldScroll != null;
    public Transform HandAnchor => _handAnchor;

    private bool _isRecording;
    private float _recordStartTime;
    private bool _whisperBusy;
    private string _feedback;
    private float _feedbackUntil;
    private WhisperManager _whisper;
    private GUIStyle _guiStyle;

    private void Update()
    {
        if (Object == null || Object.IsValid == false || HasInputAuthority == false)
            return;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null)
            return;

        if (HeldScroll == null)
        {
            if (_isRecording)
                CancelIncantation();
            return;
        }

        if (_whisperBusy)
            return;

        if (_isRecording == false && keyboard.gKey.wasPressedThisFrame)
        {
            RPC_DropScroll();
            return;
        }

        if (_isRecording == false && mouse.leftButton.wasPressedThisFrame)
        {
            StartIncantation();
        }
        else if (_isRecording && (mouse.leftButton.wasReleasedThisFrame || Time.time - _recordStartTime > _maxRecordSeconds))
        {
            _ = FinishIncantationAsync();
        }
    }

    // INCANTATION

    private void StartIncantation()
    {
        var tap = IncantationRecorder.Instance;
        if (tap == null)
        {
            ShowFeedback("Micro indisponible");
            return;
        }

        _isRecording = true;
        _recordStartTime = Time.time;

        // Les autres joueurs entendent l'incantation, même micro muté
        MicMuteControl.ForceTransmit = true;
        tap.StartCapture();
    }

    private void CancelIncantation()
    {
        _isRecording = false;
        MicMuteControl.ForceTransmit = false;
        IncantationRecorder.Instance?.StopCapture();
    }

    private async Task FinishIncantationAsync()
    {
        _isRecording = false;
        MicMuteControl.ForceTransmit = false;

        var tap = IncantationRecorder.Instance;
        if (tap == null)
            return;

        float[] samples = tap.StopCapture();
        float duration = tap.SamplingRate > 0 ? (float)samples.Length / tap.SamplingRate : 0f;

        if (duration < _minRecordSeconds)
        {
            ShowFeedback("Trop court : maintenez le clic et lisez le mot");
            return;
        }

        string expectedWord = HeldScroll != null ? HeldScroll.Word : null;
        if (expectedWord == null)
            return;

        if (_whisper == null)
            _whisper = FindFirstObjectByType<WhisperManager>();

        if (_whisper == null)
        {
            ShowFeedback("Reconnaissance vocale indisponible");
            return;
        }

        _whisperBusy = true;
        ShowFeedback("...", 10f);
        try
        {
            if (_whisper.IsLoaded == false)
                await _whisper.InitModel();

            // Whisper hallucine sur les clips très courts : on encadre le mot de
            // silence pour lui donner du contexte et garantir une durée minimale.
            samples = PadWithSilence(samples, tap.SamplingRate, 0.5f, 2.5f);

            var result = await _whisper.GetTextAsync(samples, tap.SamplingRate, 1);
            string heard = result != null ? result.Result : string.Empty;

            // Le parchemin a pu disparaître pendant la transcription
            if (HeldScroll == null)
                return;

            // Vocabulaire fermé : la transcription est rabattue sur le mot de sort
            // le plus proche parmi les 20 — on valide si c'est celui du parchemin.
            float expectedError = SpellWords.MatchError(heard, expectedWord);
            int closestIndex = SpellWords.FindClosest(heard, out float closestError);
            bool closestIsExpected = closestIndex >= 0 && SpellWords.Words[closestIndex] == expectedWord;

            if (expectedError <= _errorThreshold || (closestIsExpected && closestError <= _errorThreshold + 0.2f))
            {
                ShowFeedback($"« {expectedWord.ToUpperInvariant()} » — sort lancé !");
                RPC_CastSpell();
            }
            else
            {
                string cleaned = SpellWords.Normalize(heard);
                string closestWord = closestIndex >= 0 ? SpellWords.Words[closestIndex] : "?";
                ShowFeedback(cleaned.Length == 0
                    ? "Je n'ai rien entendu, réessayez"
                    : $"Raté... j'ai entendu « {cleaned} » (plus proche de « {closestWord} »)");
            }
        }
        finally
        {
            _whisperBusy = false;
        }
    }

    private static float[] PadWithSilence(float[] samples, int sampleRate, float padSeconds, float minTotalSeconds)
    {
        int pad = (int)(sampleRate * padSeconds);
        int total = Mathf.Max(samples.Length + pad * 2, (int)(sampleRate * minTotalSeconds));
        var padded = new float[total];
        samples.CopyTo(padded, pad);
        return padded;
    }

    // RPCs (client → serveur)

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_CastSpell()
    {
        if (HeldScroll == null)
            return;

        var scroll = HeldScroll;
        HeldScroll = null;
        Runner.Despawn(scroll.Object);

        Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;
        Vector3 origin = (_castOrigin != null ? _castOrigin.position : transform.position + Vector3.up) + direction * 0.8f;
        Runner.Spawn(_spellBallPrefab, origin, Quaternion.LookRotation(direction), Object.InputAuthority);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_DropScroll()
    {
        if (HeldScroll == null)
            return;

        Vector3 dropPosition = transform.position + transform.forward * 0.8f + Vector3.up * 0.2f;
        HeldScroll.Drop(dropPosition, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        HeldScroll = null;
    }

    // UI LOCALE

    private void ShowFeedback(string message, float seconds = 3f)
    {
        _feedback = message;
        _feedbackUntil = Time.time + seconds;
    }

    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || HasInputAuthority == false)
            return;

        if (_guiStyle == null)
        {
            _guiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        string line = null;

        if (HeldScroll != null)
        {
            line = _isRecording
                ? $"<color=red>●</color> Prononcez : <color=yellow>{HeldScroll.Word.ToUpperInvariant()}</color> (relâchez pour valider)"
                : $"Parchemin : <color=yellow>{HeldScroll.Word.ToUpperInvariant()}</color> — maintenez clic gauche et lisez le mot • G pour reposer";
        }

        if (Time.time < _feedbackUntil && string.IsNullOrEmpty(_feedback) == false)
        {
            line = line == null ? _feedback : $"{line}\n{_feedback}";
        }

        if (line == null)
            return;

        var rect = new Rect(0f, Screen.height * 0.72f, Screen.width, 60f);

        // Ombre pour la lisibilité
        var shadow = rect;
        shadow.x += 1f;
        shadow.y += 1f;
        var previousColor = GUI.color;
        GUI.color = Color.black;
        GUI.Label(shadow, line, _guiStyle);
        GUI.color = previousColor;
        GUI.Label(rect, line, _guiStyle);
    }
}
