using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGrabbing : NetworkBehaviour
{
    [SerializeField] private float grabDistance = 10.0f;
    [SerializeField] private float forcePower = 10f;
    [SerializeField] private float cooldown = 1f; 
    [SerializeField] private InputActionReference pullAction, pushAction;

    private bool _pressedOldPush, _pressedOldPull;
    private Transform _cam;
    private ScrollCaster _scrollCaster;
    private float _timerCooldown;
    private bool _canApplyForce = true;

    private void Start()
    {
        _scrollCaster = GetComponent<ScrollCaster>();

        if (pullAction != null) pullAction.action.Enable();
        if (pushAction != null) pushAction.action.Enable();

        if (Camera.main != null)
        {
            _cam = Camera.main.transform;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // On ne lit les inputs locaux que si l'on a l'autorité sur le joueur (le joueur local)
        if (!HasInputAuthority) return;

        // Parchemin en main : pas d'autre interaction avec les mains
        if (_scrollCaster != null && _scrollCaster.IsHoldingScroll) return;

        bool isPullPressed = pullAction != null && pullAction.action.IsPressed();
        bool isPushPressed = pushAction != null && pushAction.action.IsPressed();

        // Détection de l'appui (KeyDown)
        if (isPullPressed && !_pressedOldPull)
        {
            TryApplyForce(isPush: false);
        }

        if (isPushPressed && !_pressedOldPush)
        {
            TryApplyForce(isPush: true);
        }

        _pressedOldPull = isPullPressed;
        _pressedOldPush = isPushPressed;
    }

    private void Update()
    {
        if (!_canApplyForce)
        {
            _timerCooldown += Time.deltaTime;
            if (_timerCooldown > cooldown)
            {
                _canApplyForce = true;
                _timerCooldown = 0f;
            }
        }
    }

    private void TryApplyForce(bool isPush)
    {
        Vector3 rayOrigin = _cam != null ? _cam.position : transform.position + Vector3.up;
        Vector3 rayDirection = _cam != null ? _cam.forward : transform.forward;
        
        // Raycast pour détecter l'objet devant soi
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, grabDistance))
        {
            if (hit.collider.CompareTag("Grabbable") && hit.collider.TryGetComponent<NetworkRigidbody>(out var nrb))
            {
                // Calcul du vecteur de force
                Vector3 direction = (nrb.transform.position - rayOrigin).normalized;
                Debug.Log("Try Apply force");
                // Si on tire (Pull), on inverse la direction
                if (!isPush)
                {
                    direction = -direction;
                }

                Vector3 appliedForce = direction * forcePower;

                // Envoie l'ordre d'appliquer la force sur le Serveur
                RPC_ApplyForce(nrb, appliedForce);
            }
        }
    }

    // Le RPC est envoyé de l'Input Authority (le client) vers la State Authority (le serveur)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyForce(NetworkRigidbody targetNRB, Vector3 force)
    {
        Debug.Log("Apply force.");
        if (targetNRB != null && targetNRB.PhysicsBody != null && _canApplyForce)
        {
            // Le serveur applique la force sur le Rigidbody
            // NetworkRigidbody se chargera de synchroniser le mouvement chez TOUS les clients
            targetNRB.PhysicsBody.AddForce(force);
        }
    }
}