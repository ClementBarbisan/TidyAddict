using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Côté joueur : porte le parchemin et gère l'incantation. Clic gauche maintenu =
/// enregistrement du micro Photon (les autres joueurs entendent l'incantation),
/// relâché = reconnaissance Vosk en grammaire fermée (seuls les 20 mots de sort
/// peuvent sortir). Si le mot du parchemin est reconnu, le sort part et le
/// parchemin disparaît. G pour reposer le parchemin. Pendant qu'on tient un
/// parchemin, les autres interactions des mains (tir, grab) sont bloquées.
/// </summary>
public class ScrollCaster : NetworkBehaviour
{
    [SerializeField] private Transform _handAnchor;
    [SerializeField] private Transform _castOrigin;
    [SerializeField] private NetworkObject _spellBallPrefab;
    [SerializeField] private NetworkObject _iceZonePrefab;
    [SerializeField] private float _buffSeconds = 30f;
    [SerializeField] private float _stunSeconds = 10f;
    [SerializeField] private float _minRecordSeconds = 0.3f;
    [SerializeField] private float _maxRecordSeconds = 6f;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Marge d'erreur tolérée entre le mot entendu et le mot attendu (0 = strict, 1 = tout accepté)")]
    private float _errorThreshold = 0.35f;

    [Networked] public SpellScroll HeldScroll { get; set; }

    public bool IsHoldingScroll => Object != null && Object.IsValid && HeldScroll != null;
    public Transform HandAnchor => _handAnchor;

    private bool _isRecording;
    private float _recordStartTime;
    private bool _recognitionBusy;
    private string _feedback;
    private float _feedbackUntil;
    private GUIStyle _guiStyle;
    private GameObject _targetHighlight;
    private Material _targetHighlightMaterial;

    public override void Spawned()
    {
        // Précharge le modèle Vosk pour éviter la latence au premier sort
        if (HasInputAuthority)
            VoskSpellRecognizer.Preload();
    }

    private void Update()
    {
        if (Object == null || Object.IsValid == false || HasInputAuthority == false)
            return;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null)
            return;

        UpdateTargetHighlight();

        if (HeldScroll == null)
        {
            if (_isRecording)
                CancelIncantation();
            return;
        }

        if (_recognitionBusy)
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

    // SURBRILLANCE DE LA CIBLE (locale, visible uniquement par le lanceur)

    private void UpdateTargetHighlight()
    {
        PlayerSpellEffects target = null;

        if (HeldScroll != null)
        {
            var spellType = SpellWords.TypeOf(HeldScroll.WordIndex);
            if (spellType == SpellType.Confusion || spellType == SpellType.Shrink || spellType == SpellType.Stun)
                target = FindTargetPlayer();
        }

        if (target == null)
        {
            if (_targetHighlight != null && _targetHighlight.activeSelf)
                _targetHighlight.SetActive(false);
            return;
        }

        if (_targetHighlight == null)
            CreateTargetHighlight();

        _targetHighlight.SetActive(true);

        // Couleur du sort tenu
        Color color = SpellWords.ColorOf(HeldScroll.WordIndex);
        _targetHighlightMaterial.SetColor("_BaseColor", color);
        _targetHighlightMaterial.SetColor("_EmissionColor", color * 3f);

        // Anneau pulsant aux pieds de la cible
        float pulse = 1.5f + Mathf.Sin(Time.time * 6f) * 0.2f;
        _targetHighlight.transform.position = target.transform.position + Vector3.up * 0.05f;
        _targetHighlight.transform.localScale = new Vector3(pulse, 0.03f, pulse);
    }

    private void CreateTargetHighlight()
    {
        _targetHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _targetHighlight.name = "TargetHighlight";
        Destroy(_targetHighlight.GetComponent<Collider>());

        _targetHighlightMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _targetHighlightMaterial.EnableKeyword("_EMISSION");
        _targetHighlight.GetComponent<MeshRenderer>().material = _targetHighlightMaterial;
    }

    private void OnDestroy()
    {
        if (_targetHighlight != null)
            Destroy(_targetHighlight);
        if (_targetHighlightMaterial != null)
            Destroy(_targetHighlightMaterial);
        if (_isRecording)
            MicMuteControl.ForceTransmit = false;
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
            ShowFeedback("Trop court : maintenez le clic et dites le mot");
            return;
        }

        string expectedWord = HeldScroll != null ? HeldScroll.Word : null;
        if (expectedWord == null)
            return;

        _recognitionBusy = true;
        ShowFeedback("...", 10f);
        try
        {
            var alternatives = await VoskSpellRecognizer.RecognizeAsync(samples, tap.SamplingRate);

            // Le joueur ou le parchemin a pu disparaître pendant la reconnaissance
            if (Object == null || Object.IsValid == false || HeldScroll == null)
                return;

            // Marge d'erreur : on garde la meilleure hypothèse (correspondance
            // exacte = 0, sinon proximité Levenshtein token par token)
            float bestError = 1f;
            string bestHeard = null;

            foreach (string alternative in alternatives)
            {
                foreach (string token in alternative.Split(' '))
                {
                    if (token.Length == 0 || token == "[unk]")
                        continue;

                    float error = SpellWords.MatchError(token.ToLowerInvariant(), expectedWord);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestHeard = token;
                    }
                }
            }

            Debug.Log($"[ScrollCaster] attendu « {expectedWord} », entendu « {bestHeard ?? "(rien)"} » " +
                      $"(erreur {bestError:F2} / seuil {_errorThreshold:F2}) — hypothèses : {string.Join(" | ", alternatives)}");

            if (bestError <= _errorThreshold)
            {
                ShowFeedback($"« {expectedWord.ToUpperInvariant()} » — sort lancé !");
                RPC_CastSpell();
            }
            else
            {
                ShowFeedback(bestHeard == null
                    ? "Mot non reconnu — réessayez en articulant"
                    : $"Raté... j'ai entendu « {bestHeard} »");
            }
        }
        finally
        {
            _recognitionBusy = false;
        }
    }

    // RPCs (client → serveur)

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_CastSpell()
    {
        if (HeldScroll == null)
            return;

        var scroll = HeldScroll;
        int spellIndex = scroll.WordIndex;
        HeldScroll = null;
        Runner.Despawn(scroll.Object);

        CastSpell(spellIndex);
    }

    // Exécuté côté serveur : chaque mot déclenche toujours le même sort
    private void CastSpell(int spellIndex)
    {
        var effects = GetComponent<PlayerSpellEffects>();

        switch (SpellWords.TypeOf(spellIndex))
        {
            case SpellType.Fire:
            {
                source.clip = clips[0];
                Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;
                Vector3 origin = (_castOrigin != null ? _castOrigin.position : transform.position + Vector3.up) + direction * 0.8f;
                Runner.Spawn(_spellBallPrefab, origin, Quaternion.LookRotation(direction), Object.InputAuthority,
                    (runner, spawnedObject) => spawnedObject.GetComponent<SpellBall>().SpellIndex = spellIndex);
                break;
            }
            case SpellType.Ice:
            {
                if (_iceZonePrefab != null)
                {
                    source.clip = clips[1];
                    var casterRef = Object.InputAuthority;
                    Vector3 ground = new Vector3(transform.position.x, 0.02f, transform.position.z);
                    Runner.Spawn(_iceZonePrefab, ground, Quaternion.identity, casterRef,
                        (runner, spawnedObject) => spawnedObject.GetComponent<IceZone>().Caster = casterRef);
                }
                break;
            }
            case SpellType.SpeedBuff:
                if (effects != null)
                {
                    source.clip = clips[2];
                    effects.ApplySpeedBuff(_buffSeconds);
                }
                break;

            case SpellType.ForceBuff:
                if (effects != null)
                {
                    source.clip = clips[3];
                    effects.ApplyForceBuff(_buffSeconds);
                }
                break;

            case SpellType.Invisibility:
                if (effects != null)
                {
                    source.clip = clips[4];
                    effects.ApplyInvisibility(_buffSeconds);
                }
                break;

            case SpellType.Confusion:
            {
                var target = FindTargetPlayer();
                if (target != null)
                {
                    source.clip = clips[4];
                    target.ApplyConfusion(_buffSeconds);
                }
                break;
            }
            case SpellType.Shrink:
            {
                var target = FindTargetPlayer();
                if (target != null)
                {
                    source.clip = clips[5];
                    target.ApplyShrink(_buffSeconds);
                }
                break;
            }
            case SpellType.Stun:
            {
                var target = FindTargetPlayer();
                if (target != null)
                {
                    source.clip = clips[6];
                    target.ApplyStun(_stunSeconds);
                }
                break;
            }
        }
    }

    // Cible des sorts offensifs : le joueur visé (spherecast depuis la caméra),
    // sinon le joueur le plus proche dans un rayon de 8 m. Jamais soi-même.
    private PlayerSpellEffects FindTargetPlayer()
    {
        Vector3 origin = _castOrigin != null ? _castOrigin.position : transform.position + Vector3.up;
        Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;

        if (Physics.SphereCast(origin, 1.2f, direction, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
        {
            var aimed = hit.collider.GetComponentInParent<PlayerSpellEffects>();
            if (aimed != null && aimed != GetComponent<PlayerSpellEffects>())
                return aimed;
        }

        PlayerSpellEffects closest = null;
        float closestDistance = 8f;
        var self = GetComponent<PlayerSpellEffects>();

        foreach (var candidateCollider in Physics.OverlapSphere(transform.position, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            var candidate = candidateCollider.GetComponentInParent<PlayerSpellEffects>();
            if (candidate == null || candidate == self)
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
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
            // Couleur et description du sort : on sait ce qu'on lance avant de le dire
            string hex = ColorUtility.ToHtmlStringRGB(SpellWords.ColorOf(HeldScroll.WordIndex));
            string coloredWord = $"<color=#{hex}>{HeldScroll.Word.ToUpperInvariant()}</color>";
            string description = SpellWords.DescriptionOf(HeldScroll.WordIndex);

            line = _isRecording
                ? $"<color=red>●</color> Dites : {coloredWord} (relâchez pour valider)\n<size=13>{description}</size>"
                : $"Parchemin : {coloredWord} — <size=13>{description}</size>\nMaintenez clic gauche et dites le mot • G pour reposer";
        }

        if (Time.time < _feedbackUntil && string.IsNullOrEmpty(_feedback) == false)
        {
            line = line == null ? _feedback : $"{line}\n{_feedback}";
        }

        if (line == null)
            return;

        var rect = new Rect(0f, Screen.height * 0.72f, Screen.width, 60f);

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
