using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Caméra fantôme pour les spectateurs (équipes pleines) : vol libre avec
/// ZQSD/WASD, Espace pour monter, Ctrl pour descendre, Shift pour accélérer,
/// Échap pour libérer la souris. Purement local : le personnage réseau a été
/// despawné, personne ne voit ni n'entend le spectateur.
/// </summary>
public class SpectatorController : MonoBehaviour
{
    private const float MoveSpeed = 8f;
    private const float FastMultiplier = 2.5f;
    private const float LookSensitivity = 0.12f;

    private Transform _camera;
    private Vector2 _look;

    public static void Activate()
    {
        var go = new GameObject("SpectatorController");
        DontDestroyOnLoad(go);
        go.AddComponent<SpectatorController>();
    }

    private void Start()
    {
        TryAttachCamera();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void TryAttachCamera()
    {
        if (Camera.main == null)
            return;

        _camera = Camera.main.transform;

        // Point de départ : un peu au-dessus de là où était la caméra
        _camera.position += Vector3.up * 3f;
        var euler = _camera.rotation.eulerAngles;
        _look = new Vector2(euler.x > 180f ? euler.x - 360f : euler.x, euler.y);
    }

    private void Update()
    {
        if (_camera == null)
        {
            TryAttachCamera();
            return;
        }

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null)
            return;

        // Échap : libérer/reprendre la souris
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // Regard souris
        Vector2 delta = mouse.delta.ReadValue() * LookSensitivity;
        _look.x = Mathf.Clamp(_look.x - delta.y, -89f, 89f);
        _look.y += delta.x;
        _camera.rotation = Quaternion.Euler(_look.x, _look.y, 0f);

        // Déplacement libre (les touches sont physiques : ZQSD sur clavier AZERTY)
        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += _camera.forward;
        if (keyboard.sKey.isPressed) move -= _camera.forward;
        if (keyboard.aKey.isPressed) move -= _camera.right;
        if (keyboard.dKey.isPressed) move += _camera.right;
        if (keyboard.spaceKey.isPressed) move += Vector3.up;
        if (keyboard.leftCtrlKey.isPressed) move -= Vector3.up;

        if (move == Vector3.zero)
            return;

        float speed = MoveSpeed * (keyboard.leftShiftKey.isPressed ? FastMultiplier : 1f);
        _camera.position += move.normalized * speed * Time.deltaTime;
    }
}
