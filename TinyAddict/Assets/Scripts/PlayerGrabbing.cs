using System;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGrabbing : NetworkBehaviour
{
    public Transform HoldPoint;
    [SerializeField] private float grabDistance = 3.0f;
    [SerializeField] private float _force = 10;
    [SerializeField] private InputActionReference _pull, _push;
    private bool _pressedOldPush, _pressedOldPull;
    private Transform _cam;
    
    private void Start()
    {
        _pull.action.Enable();
        _push.action.Enable();
        _cam = Camera.main.transform;
    }

    public override void FixedUpdateNetwork()
    {
        // On ne gère l'action que si on possède l'autorité sur le joueur
        if (_pull.action.IsPressed() && _pressedOldPull == false)
        {
            // Exemple : On appuie sur la touche Interaction (ex: 'E' ou clic)
            TryPullObject();
        }
        if (_push.action.IsPressed() && _pressedOldPush == false)
        {
            TryPushObject();
        }
        _pressedOldPull = _pull.action.IsPressed();
        _pressedOldPush = _push.action.IsPressed();
    }

    private void TryPullObject()
    {
        // Raycast pour détecter l'objet devant soi
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit hit, grabDistance))
        {
            if (hit.collider.CompareTag("Grabbable") && hit.collider.TryGetComponent<NetworkRigidbody>(out var rb))
            {
                rb.PhysicsBody.AddForce((_cam.position - rb.transform.position) * _force);
            }
        }
    }
    
    private void TryPushObject()
    {
        // Raycast pour détecter l'objet devant soi
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit hit, grabDistance))
        {
            if (hit.collider.CompareTag("Grabbable") && hit.collider.TryGetComponent<NetworkRigidbody>(out var rb))
            {
                rb.PhysicsBody.AddForce((_cam.position - rb.transform.position) * _force);
            }
        }
    }
    
}