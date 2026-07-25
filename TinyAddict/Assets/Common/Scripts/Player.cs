using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;

namespace Projectiles
{
    [DefaultExecutionOrder(-5)]
    public class Player : NetworkBehaviour
    {
        // PRIVATE MEMBERS

        [SerializeField]
        private float _moveSpeed = 10f;
        [SerializeField]
        private float _jumpImpulse = 6f;
        [SerializeField]
        private Transform _cameraPivot;
        [SerializeField]
        private SkinnedMeshRenderer[] _thirdPersonRenderers;
        [SerializeField]
        private Animator _animator;
        [Networked]
        private Vector2 _animMoveVelocity { get; set; }

        [Networked]
        private NetworkButtons _lastButtonsInput { get; set; }
        
        [Networked]
        private byte _jumpTriggerCount { get; set; }
        [Networked]
        private byte _onHitTriggerCount { get; set; }
        [Networked]
        private byte _throwTriggerCount { get; set; }

        private byte _renderedJumpTriggerCount;
        private byte _renderedOnHitTriggerCount;
        private byte _renderedThrowTriggerCount;

        private SimpleKCC _kcc;
        private PlayerInput _input;
        private Transform _cameraTransform;
        private ScrollCaster _scrollCaster;
        private PlayerSpellEffects _spellEffects;
        private static readonly int X = Animator.StringToHash("X");
        private static readonly int Y = Animator.StringToHash("Y");

        // NetworkBehaviour INTERFACE

        public override void Spawned()
        {
            if (HasInputAuthority && _cameraTransform == null)
            {
                var scene = Runner.SimulationUnityScene.GetComponent<Scene>();
                _cameraTransform = Camera.main.transform;
                TeamManager.Instance.SetPlayerTeam(GetComponent<NetworkObject>().InputAuthority, Team.None);
                // Look rotation interpolation is skipped for the local player - it is set manually
                // in Render from the accumulated (absolute) look rotation for a smooth camera.
                _kcc.Settings.ForcePredictedLookRotation = true;

                // Seed the input with the KCC's spawn look rotation so the absolute look rotation
                // starts where the player spawned instead of snapping to zero on the first input.
                _input.SetLookRotation(_kcc.GetLookRotation(true, true));
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput<GameplayInput>(out var input))
            {
                ProcessInput(input);
            }
            else
            {
                // Keep the KCC updated (gravity, momentum) even when input is missing.
                // Look rotation is intentionally left untouched so it is not snapped to zero.
                _kcc.Move(Vector3.zero);
            }
        }

        public override void Render()
        {
            if (HasInputAuthority == false)
                return;

            // Set the absolute look rotation for Render so the camera reacts every rendered frame.
            _kcc.SetLookRotation(_input.LookRotation, -90f, 90f);
            
            _animator.SetFloat(X, _animMoveVelocity.x);
            _animator.SetFloat(Y, _animMoveVelocity.y);
            
            if (_jumpTriggerCount != _renderedJumpTriggerCount)
            {
                _renderedJumpTriggerCount = _jumpTriggerCount;
                _animator.SetTrigger("Jump");
            }

            if (_onHitTriggerCount != _renderedOnHitTriggerCount)
            {
                _renderedOnHitTriggerCount = _onHitTriggerCount;
                _animator.SetTrigger("OnHit");
            }

            if (_throwTriggerCount != _renderedThrowTriggerCount)
            {
                _renderedThrowTriggerCount = _throwTriggerCount;
                _animator.SetTrigger("Throw");
            }
        }

        // MONOBEHAVIOUR

        protected void Awake()
        {
            _kcc = GetComponent<SimpleKCC>();
            _input = GetComponent<PlayerInput>();
            _scrollCaster = GetComponent<ScrollCaster>();
            _spellEffects = GetComponent<PlayerSpellEffects>();
            _kcc.SetGravity(-20f);
        }

        protected void LateUpdate()
        {
            if (HasInputAuthority == false)
                return;

            // Update camera pitch (KCC look rotation is set earlier in Render / FixedUpdateNetwork)
            Vector2 pitchRotation = _kcc.GetLookRotation(true, false);
            _cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            if (_cameraTransform != null)
            {
                _cameraTransform.position = _cameraPivot.position;
                _cameraTransform.rotation = _cameraPivot.rotation;
            }

            // Hide meshes that should be visible only for third person players
            for (int i = 0; i < _thirdPersonRenderers.Length; i++)
            {
                _thirdPersonRenderers[i].enabled = Runner.GetVisible() && HasInputAuthority == false;
            }
        }

        // PRIVATE METHODS

        private void ProcessInput(GameplayInput input)
        {
            _kcc.SetLookRotation(input.LookRotation, -90f, 90f);

            // Direction horizontale
            Vector2 moveDirection = input.MoveDirection;

            // Sort electra : paralysé, aucun déplacement volontaire
            bool isStunned = _spellEffects != null && _spellEffects.IsStunned;
            if (isStunned)
                moveDirection = Vector2.zero;

            // Sort vertigo : les touches de déplacement sont inversées
            if (_spellEffects != null && _spellEffects.IsConfused)
                moveDirection = -moveDirection;

            Vector3 inputDirection = _kcc.TransformRotation * new Vector3(moveDirection.x, 0f, moveDirection.y);
            Vector3 moveVelocity = inputDirection * _moveSpeed;
            
            _animMoveVelocity = input.MoveDirection;

            float   jumpImpulse  = default;
            // Gestion du saut/gravité (vélocité verticale gérée manuellement)
            if (_kcc.IsGrounded)
            {
                if (isStunned == false && input.Buttons.WasPressed(_lastButtonsInput, EInputButtons.Jump))
                {
                    jumpImpulse = _jumpImpulse;
                    _jumpTriggerCount++;
                }
            }

            // Effets de sort : ralentissement/buff de vitesse et éjection d'explosion
            if (_spellEffects != null)
            {
                moveVelocity *= _spellEffects.SpeedMultiplier;

                Vector3 knockback = _spellEffects.CurrentKnockback;
                moveVelocity += new Vector3(knockback.x, 0f, knockback.z);

                // La composante verticale de l'éjection n'est appliquée qu'au premier
                // tick (impulsion one-shot, comme un saut)
                if (knockback.y > 0f && _spellEffects.IsKnockbackFresh(Runner.DeltaTime))
                {
                    jumpImpulse = Mathf.Max(jumpImpulse, knockback.y);
                }
            }

            _kcc.Move(moveVelocity, jumpImpulse);

            // Update fire transform before fire
            Vector2 pitchRotation = _kcc.GetLookRotation(true, false);
            _cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            _lastButtonsInput = input.Buttons;
        }
    }
}
