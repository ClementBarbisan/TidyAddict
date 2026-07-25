using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class PlayerGrabbing : NetworkBehaviour
{
    [SerializeField] private float grabDistance = 10.0f;
    [SerializeField] private float forcePower = 10f;
    [SerializeField] private float cooldown = 1f; 
    [SerializeField] private InputActionReference pullAction, pushAction;
    [SerializeField] private EffectPullPush effect;

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
        if (!HasInputAuthority) return;

        // Parchemin en main : pas d'autre interaction avec les mains
        if (_scrollCaster != null && _scrollCaster.IsHoldingScroll) return;

        bool isPullPressed = pullAction != null && pullAction.action.WasPressedThisFrame();
        bool isPushPressed = pushAction != null && pushAction.action.WasPressedThisFrame();

        // Détection de l'appui (KeyDown)
        if (isPullPressed)
        {
            TryApplyForce(isPush: false);
        }

        if (isPushPressed)
        {
            TryApplyForce(isPush: true);
        }
    }

    private void TryApplyForce(bool isPush)
    {
        if (!_canApplyForce)
            return;
        
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

                // Sort maximus : force démultipliée pendant la durée du buff
                float forceMultiplier = 1f;
                var spellEffects = GetComponent<PlayerSpellEffects>();
                if (spellEffects != null)
                    forceMultiplier = spellEffects.ForceMultiplier;

                Vector3 appliedForce = direction * forcePower * forceMultiplier;

                // Envoie l'ordre d'appliquer la force sur le Serveur
                RPC_ApplyForce(nrb, appliedForce);
                effect.ShowBeam(_cam.position + _cam.forward * grabDistance, isPush);
            }
        }
    }

    // Le RPC est envoyé de l'Input Authority (le client) vers la State Authority (le serveur)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyForce(NetworkRigidbody targetNRB, Vector3 force)
    {
        Debug.Log("Apply force.");
        if (targetNRB != null && targetNRB.PhysicsBody != null)
        {
            // Le serveur applique la force sur le Rigidbody
            // NetworkRigidbody se chargera de synchroniser le mouvement chez TOUS les clients
            targetNRB.PhysicsBody.AddForce(force);
            _canApplyForce = false;
        }
    }
}