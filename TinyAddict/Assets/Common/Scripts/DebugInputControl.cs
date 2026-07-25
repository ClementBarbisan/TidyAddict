using Fusion;
using UnityEngine;

namespace Projectiles
{
    public class DebugInputControl : NetworkBehaviour
    {
        // PUBLIC MEMBERS

        public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        // PRIVATE MEMBERS

        private static int _lastSingleInputChange;
        private static int _cursorLockRequests;

        // InputActions is auto-generated from the InputSystem_Actions asset.
        private InputActions _inputActions;

        // PUBLIC METHODS

        public void RequestCursorLock()
        {
            // Static requests count is used for multi-peer setup
            _cursorLockRequests++;

            if (_cursorLockRequests == 1)
            {
                // First lock request, let's lock
                SetLockedState(true);
            }
        }

        public void RequestCursorRelease()
        {
            _cursorLockRequests--;

            Assert.Check(_cursorLockRequests >= 0, "Cursor lock requests are negative, this should not happen");

            if (_cursorLockRequests == 0)
            {
                SetLockedState(false);
            }
        }

        // NetworkBehaviour INTERFACE

        public override void Render()
        {
            // Only one single input change per frame is possible (important for multi-peer multi-input game)
            if (_lastSingleInputChange == Time.frameCount)
                return;

            if (_inputActions.Player.Escape.WasPressedThisFrame())
            {
                SetLockedState(Cursor.lockState != CursorLockMode.Locked);
                _lastSingleInputChange = Time.frameCount;
            }

            if (_inputActions.Player.Client0.WasPressedThisFrame())
            {
                SetActiveRunner(-1);
            }
            else if (_inputActions.Player.Client1.WasPressedThisFrame())
            {
                SetActiveRunner(0);
            }
            else if (_inputActions.Player.Client2.WasPressedThisFrame())
            {
                SetActiveRunner(1);
            }
            else if (_inputActions.Player.Client3.WasPressedThisFrame())
            {
                SetActiveRunner(2);
            }
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

        private void SetLockedState(bool value)
        {
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;

            //Debug.Log($"Cursor lock state {Cursor.lockState}, visibility {Cursor.visible}");
        }

        private void SetActiveRunner(int index)
        {
            var enumerator = NetworkRunner.GetInstancesEnumerator();

            int currentIndex = -1;
            while (enumerator.MoveNext())
            {
                var runner = enumerator.Current;

                // Skip temporary runner
                if (runner.LocalPlayer.IsRealPlayer == false)
                    continue;

                currentIndex++;

                runner.SetVisible(index < 0 || currentIndex == index);
                runner.ProvideInput = index < 0 || currentIndex == index;
            }
        }
    }
}
