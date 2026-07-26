using System;
using System.Threading.Tasks;
using Fusion;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// Côté joueur : porte le parchemin et gère l'incantation. Clic gauche maintenu =
/// enregistrement du micro Photon (les autres joueurs entendent l'incantation),
/// relâché = reconnaissance Vosk en grammaire fermée (seuls les 20 mots de sort
/// peuvent sortir). Si le mot du parchemin est reconnu, le sort part et le
/// parchemin disparaît. G pour reposer le parchemin. Pendant qu'on tient un
/// parchemin, les autres interactions des mains (tir, grab) sont bloquées.
/// </summary>
///
///
namespace Projectiles
{
    public class ScrollCaster : NetworkBehaviour
    {
        [SerializeField] private Transform _handAnchor;
        [SerializeField] private Transform _castOrigin;
        [SerializeField] private NetworkObject _spellBallPrefab;
        [SerializeField] private NetworkObject _iceZonePrefab;
        [SerializeField] private NetworkObject _wallPrefab;
        [SerializeField] private NetworkObject _blackHolePrefab;
        [SerializeField] private float _buffSeconds = 30f;
        [SerializeField] private float _stunSeconds = 10f;
        [SerializeField] private float _minRecordSeconds = 0.3f;
        [SerializeField] private float _maxRecordSeconds = 6f;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip[] clips;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Marge d'erreur tolérée entre le mot entendu et le mot attendu (0 = strict, 1 = tout accepté)")]
        private float _errorThreshold = 0.35f;

        private Player _player;
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

        private void Awake()
        {
            _player = GetComponent<Player>();
        }

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
            else if (_isRecording && (mouse.leftButton.wasReleasedThisFrame ||
                                      Time.time - _recordStartTime > _maxRecordSeconds))
            {
                _ = FinishIncantationAsync();
            }
        }

        // SURBRILLANCE DE LA CIBLE (locale, visible uniquement par le lanceur)

        private void UpdateTargetHighlight()
        {
            Transform target = null;

            if (HeldScroll != null)
            {
                var spellType = SpellWords.TypeOf(HeldScroll.WordIndex);
                if (spellType == SpellType.Confusion || spellType == SpellType.Shrink || spellType == SpellType.Stun)
                {
                    var player = FindTargetPlayer();
                    if (player != null)
                        target = player.transform;
                }
                else if (spellType == SpellType.Loot)
                {
                    // fortuna : on surligne l'objet qui sera téléporté dans notre zone
                    var item = FindTargetGrabbable();
                    if (item != null)
                        target = item.transform;
                }
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
                    ShowFeedback($"◆  <b>{expectedWord.ToUpperInvariant()}</b> — sort lancé !", 2.5f, isError: false,
                        SpellWords.ColorOf(HeldScroll.WordIndex));
                    RPC_CastSpell();
                }
                else
                {
                    ShowFeedback(bestHeard == null
                        ? "✗  Mot non reconnu — réessayez en articulant"
                        : $"✗  Raté… j'ai entendu <i>« {bestHeard} »</i>", 2.5f, isError: true, default);
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

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_TriggerThrowEffect()
        {
            _player.TriggerThrowAnimation();
        }

        // Exécuté côté serveur : chaque mot déclenche toujours le même sort
        private void CastSpell(int spellIndex)
        {
            var effects = GetComponent<PlayerSpellEffects>();

            RPC_TriggerThrowEffect();

            switch (SpellWords.TypeOf(spellIndex))
            {
                case SpellType.Fire:
                {
                    source.clip = clips[0];
                    Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;
                    Vector3 origin = (_castOrigin != null ? _castOrigin.position : transform.position + Vector3.up) +
                                     direction * 0.8f;
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
                case SpellType.BlackHole:
                {
                    if (_blackHolePrefab != null)
                    {
                        // Le trou noir apparaît là où on regarde (mur/sol), sinon 12 m devant
                        Vector3 origin = _castOrigin != null ? _castOrigin.position : transform.position + Vector3.up;
                        Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;

                        Vector3 point;
                        if (Physics.Raycast(origin, direction, out RaycastHit blackHoleHit, 25f, ~0,
                                QueryTriggerInteraction.Ignore))
                            point = blackHoleHit.point;
                        else
                            point = origin + direction * 12f;
                        point.y = Mathf.Max(point.y, 1f);

                        var casterRef = Object.InputAuthority;
                        Runner.Spawn(_blackHolePrefab, point, Quaternion.identity, casterRef,
                            (runner, spawnedObject) => spawnedObject.GetComponent<BlackHoleZone>().Caster = casterRef);
                    }

                    break;
                }
                case SpellType.Loot:
                {
                    // L'objet visé se téléporte directement dans NOTRE zone de collecte
                    var item = FindTargetGrabbable();
                    if (item != null && GameState.Instance != null && TeamManager.Instance != null)
                    {
                        Team myTeam = TeamManager.Instance.GetPlayerTeam(Object.InputAuthority);
                        if (myTeam != Team.None)
                        {
                            Vector3 destination = GameState.Instance.GetZoneCenter(myTeam)
                                                  + Vector3.up * 1.5f
                                                  + new Vector3(Random.Range(-1.2f, 1.2f), 0f,
                                                      Random.Range(-1.2f, 1.2f));

                            var networkRigidbody = item.GetComponent<Fusion.Addons.Physics.NetworkRigidbody>();
                            if (networkRigidbody != null)
                            {
                                // Stoppe l'élan avant la téléportation
                                if (networkRigidbody.PhysicsBody != null)
                                {
                                    networkRigidbody.PhysicsBody.LinearVelocity = Vector3.zero;
                                    networkRigidbody.PhysicsBody.AngularVelocity = Vector3.zero;
                                }

                                networkRigidbody.Teleport(destination);
                            }
                            else
                            {
                                item.transform.position = destination;
                            }
                        }
                    }

                    break;
                }
                case SpellType.Charge:
                {
                    if (effects != null)
                    {
                        Vector3 chargeDirection = _castOrigin != null ? _castOrigin.forward : transform.forward;
                        chargeDirection.y = 0f;
                        if (chargeDirection.sqrMagnitude > 0.01f)
                            effects.ApplyCharge(chargeDirection.normalized);
                    }

                    break;
                }
                case SpellType.Wall:
                {
                    if (_wallPrefab != null)
                    {
                        // Mur perpendiculaire au regard, 3 m devant le lanceur, au sol
                        Vector3 forward = _castOrigin != null ? _castOrigin.forward : transform.forward;
                        forward.y = 0f;
                        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : transform.forward;

                        Vector3 position = new Vector3(
                            transform.position.x + forward.x * 3f,
                            0f,
                            transform.position.z + forward.z * 3f);

                        Runner.Spawn(_wallPrefab, position, Quaternion.LookRotation(forward), Object.InputAuthority);
                    }

                    break;
                }
            }

            source.Play();
        }

        // Cible du sort fortuna : l'objet Grabbable visé (spherecast depuis la caméra)
        private GameObject FindTargetGrabbable()
        {
            Vector3 origin = _castOrigin != null ? _castOrigin.position : transform.position + Vector3.up;
            Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;

            if (Physics.SphereCast(origin, 0.8f, direction, out RaycastHit hit, 30f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                var root = hit.collider.attachedRigidbody != null
                    ? hit.collider.attachedRigidbody.gameObject
                    : hit.collider.gameObject;
                if (root.CompareTag("Grabbable"))
                    return root;
            }

            return null;
        }

        // Cible des sorts offensifs : le joueur visé (spherecast depuis la caméra),
        // sinon le joueur le plus proche dans un rayon de 8 m. Jamais soi-même.
        private PlayerSpellEffects FindTargetPlayer()
        {
            Vector3 origin = _castOrigin != null ? _castOrigin.position : transform.position + Vector3.up;
            Vector3 direction = _castOrigin != null ? _castOrigin.forward : transform.forward;

            if (Physics.SphereCast(origin, 1.2f, direction, out RaycastHit hit, 30f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                var aimed = hit.collider.GetComponentInParent<PlayerSpellEffects>();
                if (aimed != null && aimed != GetComponent<PlayerSpellEffects>())
                    return aimed;
            }

            PlayerSpellEffects closest = null;
            float closestDistance = 8f;
            var self = GetComponent<PlayerSpellEffects>();

            foreach (var candidateCollider in Physics.OverlapSphere(transform.position, 8f, ~0,
                         QueryTriggerInteraction.Ignore))
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

        // UI LOCALE (design system : bandeau d'incantation + toasts)

        private bool _feedbackIsError;
        private Color _feedbackColor;
        private GUIStyle _wordStyle;
        private GUIStyle _wordListenStyle;
        private GUIStyle _descStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _toastStyle;

        private void ShowFeedback(string message, float seconds = 3f, bool isError = false, Color spellColor = default)
        {
            _feedback = message;
            _feedbackUntil = Time.time + seconds;
            _feedbackIsError = isError;
            _feedbackColor = spellColor == default ? UITheme.Parchment : spellColor;
        }

        private void OnGUI()
        {
            if (Object == null || Object.IsValid == false || HasInputAuthority == false)
                return;

            UITheme.Begin();
            EnsureUiStyles();

            float centerX = UITheme.VirtualWidth * 0.5f;
            float bandBottom = UITheme.VirtualHeight - 36f;

            // BANDEAU D'INCANTATION (bas-centre, bord 1.5 couleur du sort)
            if (HeldScroll != null)
            {
                Color spellColor = SpellWords.ColorOf(HeldScroll.WordIndex);
                string word = HeldScroll.Word.ToUpperInvariant();
                string description = SpellWords.DescriptionOf(HeldScroll.WordIndex);

                var band = new Rect(centerX - 380f, bandBottom - 108f, 760f, 108f);
                UITheme.DrawRounded(band, UITheme.WithAlpha(UITheme.PanelHud, 0.88f), 16f);
                UITheme.DrawBorder(band, UITheme.WithAlpha(spellColor, 0.8f), 1.5f, 16f);

                if (_isRecording)
                {
                    // Point rouge pulsant + mot en grand
                    float pulse = 0.55f + Mathf.PingPong(Time.time * 1.6f, 0.45f);
                    UITheme.DrawRounded(new Rect(band.x + 34f, band.y + 34f, 16f, 16f),
                        UITheme.WithAlpha(UITheme.Danger, pulse), 8f);

                    _wordListenStyle.normal.textColor = spellColor;
                    GUI.Label(new Rect(band.x + 66f, band.y + 12f, band.width - 100f, 56f), $"Dites :  {word}",
                        _wordListenStyle);
                    _hintStyle.normal.textColor = UITheme.TextDim;
                    GUI.Label(new Rect(band.x, band.y + 70f, band.width, 24f), "RELÂCHEZ POUR VALIDER", _hintStyle);
                }
                else
                {
                    _wordStyle.normal.textColor = spellColor;
                    GUI.Label(new Rect(band.x + 32f, band.y + 12f, band.width - 64f, 42f),
                        $"◆  Parchemin :  {word}", _wordStyle);
                    _descStyle.normal.textColor = UITheme.Parchment;
                    GUI.Label(new Rect(band.x + 32f, band.y + 52f, band.width - 64f, 24f), description, _descStyle);
                    _hintStyle.normal.textColor = UITheme.TextDim;
                    GUI.Label(new Rect(band.x + 32f, band.y + 78f, band.width - 64f, 22f),
                        "MAINTENEZ CLIC GAUCHE ET DITES LE MOT  •  G POUR REPOSER", _hintStyle);
                }
            }

            // TOAST DE RÉSULTAT (au-dessus du bandeau)
            if (Time.time < _feedbackUntil && string.IsNullOrEmpty(_feedback) == false)
            {
                Color borderColor = _feedbackIsError ? UITheme.Danger : _feedbackColor;
                var content = new GUIContent(_feedback);
                float width = _toastStyle.CalcSize(content).x + 48f;
                var toast = new Rect(centerX - width * 0.5f, bandBottom - 172f, width, 48f);

                UITheme.DrawRounded(toast, UITheme.WithAlpha(UITheme.PanelHud, 0.9f), 12f);
                UITheme.DrawBorder(toast, UITheme.WithAlpha(borderColor, 0.85f), 1.5f, 12f);
                _toastStyle.normal.textColor = _feedbackIsError ? UITheme.Danger : UITheme.Parchment;
                GUI.Label(toast, content, _toastStyle);
            }
        }

        private void EnsureUiStyles()
        {
            if (_wordStyle != null)
                return;

            _wordStyle = UITheme.Label(UITheme.Display, 30, UITheme.Parchment, TextAnchor.MiddleLeft);
            _wordListenStyle = UITheme.Label(UITheme.Display, 38, UITheme.Parchment, TextAnchor.MiddleLeft);
            _descStyle = UITheme.Label(UITheme.Body, 18, UITheme.Parchment, TextAnchor.MiddleLeft);
            _hintStyle = UITheme.Label(UITheme.BodyExtraBold, 13, UITheme.TextDim, TextAnchor.MiddleCenter);
            _toastStyle = UITheme.Label(UITheme.BodyBold, 19, UITheme.Parchment, TextAnchor.MiddleCenter);
        }
    }
}