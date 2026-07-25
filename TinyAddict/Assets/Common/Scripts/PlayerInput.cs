using Fusion;
using UnityEngine;

namespace Projectiles
{
    public enum EInputButtons
    {
        Fire = 0,
        Jump = 1,
    }

    public struct GameplayInput : INetworkInput
    {
        public Vector2        MoveDirection;
        public Vector2        LookRotation;
        public NetworkButtons Buttons;
    }

    public class PlayerInput : NetworkBehaviour, IBeforeUpdate
    {
        // PUBLIC MEMBERS

        // Absolute look rotation (pitch, yaw) accumulated from mouse/gamepad. Used by Player in Render.
        public Vector2 LookRotation => _lookRotation;

        // PRIVATE MEMBERS

        [SerializeField]
        private DebugInputControl _inputControl;

        // Accumulated input holds combined input for all render frames from last fixed update
        private GameplayInput _accumulatedInput;

        private bool _resetCachedInput;

        // Look rotation is absolute so it is stored separately - it must persist across input resets
        private Vector2 _lookRotation;

        // InputActions is auto-generated from the InputSystem_Actions asset.
        private InputActions _inputActions;

        // PUBLIC METHODS

        // Sets the absolute look rotation directly. Used to seed the initial look rotation
        // (e.g. from the KCC on spawn) so switching to absolute input does not snap the view.
        public void SetLookRotation(Vector2 lookRotation)
        {
            _lookRotation = lookRotation;
            _accumulatedInput.LookRotation = lookRotation;
        }

        // NetworkBehaviour INTERFACE

        public override void Spawned()
        {
            // Reset to default state (in case this object was cached)
            _accumulatedInput = default;

            if (Runner.LocalPlayer == Object.InputAuthority)
            {
                var events = Runner.GetComponent<NetworkEvents>();

                events.OnInput.RemoveListener(OnInput);
                events.OnInput.AddListener(OnInput);

                _inputControl.RequestCursorLock();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            var events = Runner.GetComponent<NetworkEvents>();
            events.OnInput.RemoveListener(OnInput);

            if (Runner.LocalPlayer == Object.InputAuthority)
            {
                _inputControl.RequestCursorRelease();
            }
        }

        // IBeforeUpdate INTERFACE

        void IBeforeUpdate.BeforeUpdate()
        {
            if (HasInputAuthority == false)
                return;

            if (_resetCachedInput)
            {
                _accumulatedInput = default;
                _resetCachedInput = false;
            }

            // Look rotation is absolute - keep it in the accumulated input even when the reset above
            // cleared it or when input processing below is skipped (e.g. cursor not locked).
            _accumulatedInput.LookRotation = _lookRotation;

            // Input is tracked only if the runner should provide input (important in multipeer mode)
            if (Runner.ProvideInput == false || _inputControl.IsLocked == false)
                return;

            ProcessInput();
        }

        // MONOBEHAVIOUR

        private void OnEnable()
        {
            _inputActions ??= new InputActions();
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }

        // PRIVATE METHODS

        private void OnInput(NetworkRunner runner, NetworkInput networkInput)
        {
            // Input is polled for single fixed update, but at this time we don't know how many times in a row OnInput() will be executed.
            // This is the reason for having a reset flag instead of resetting input immediately, otherwise we could lose input for next
            // fixed updates (for example move direction).
            _resetCachedInput = true;

            networkInput.Set(_accumulatedInput);

            // Look rotation is absolute so - unlike a relative delta - it must NOT be reset here.
            // It is preserved in _lookRotation and re-applied to the accumulated input every BeforeUpdate.
        }

        private void ProcessInput()
        {
            if (_inputActions.Player.Fire.IsPressed())
            {
                _accumulatedInput.Buttons.Set(EInputButtons.Fire, true);
            }

            if (_inputActions.Player.Jump.WasPressedThisFrame())
            {
                _accumulatedInput.Buttons.Set(EInputButtons.Jump, true);
            }

            _accumulatedInput.MoveDirection = _inputActions.Player.Move.ReadValue<Vector2>();

            var lookValue = _inputActions.Player.Look.ReadValue<Vector2>();
            _lookRotation += new Vector2(-lookValue.y, lookValue.x);
            _accumulatedInput.LookRotation = _lookRotation;
        }
    }
}
