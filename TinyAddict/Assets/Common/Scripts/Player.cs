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
        private MeshRenderer[] _thirdPersonRenderers;

        [Networked]
        private NetworkButtons _lastButtonsInput { get; set; }

        private SimpleKCC _kcc;
        private PlayerInput _input;
        private Transform _cameraTransform;

        // NetworkBehaviour INTERFACE

        public override void Spawned()
        {
            if (HasInputAuthority && _cameraTransform == null)
            {
                var scene = Runner.SimulationUnityScene.GetComponent<Scene>();
                _cameraTransform = Camera.main.transform;

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
        }

        // MONOBEHAVIOUR

        protected void Awake()
        {
            _kcc = GetComponent<SimpleKCC>();
            _input = GetComponent<PlayerInput>();
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
            Vector3 inputDirection = _kcc.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            Vector3 moveVelocity = inputDirection * _moveSpeed;
            float   jumpImpulse  = default;
            // Gestion du saut/gravité (vélocité verticale gérée manuellement)
            if (_kcc.IsGrounded)
            {
                if (input.Buttons.WasPressed(_lastButtonsInput, EInputButtons.Jump))
                {
                    jumpImpulse = _jumpImpulse;
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
