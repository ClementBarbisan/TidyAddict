using Fusion;
using Fusion.Addons.Physics;
using Projectiles;
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
    private Player _player;

    private void Start()
    {
        _player = GetComponent<Player>();
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

    if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, grabDistance))
    {
        if (hit.collider.CompareTag("Grabbable") && hit.collider.TryGetComponent<NetworkRigidbody>(out var nrb))
        {
            Vector3 direction = (nrb.transform.position - rayOrigin).normalized;
            if (!isPush)
            {
                direction = -direction;
            }
            else
            {
                direction *= 1.5f;
            }

            float forceMultiplier = 1f;
            var spellEffects = GetComponent<PlayerSpellEffects>();
            if (spellEffects != null)
                forceMultiplier = spellEffects.ForceMultiplier;

            Vector3 appliedForce = direction * forcePower * forceMultiplier;

            // Envoie l'ordre au serveur - PAS d'appel local direct à effect/anim ici,
            // le serveur se chargera de les propager à tout le monde.
            RPC_ApplyForce(nrb, appliedForce, isPush);

            // Petit gate client-side pour l'UX (évite le spam visuel avant la réponse du serveur)
            _canApplyForce = false;
        }
    }
}

// Le RPC est envoyé de l'Input Authority (le client) vers la State Authority (le serveur)
[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
private void RPC_ApplyForce(NetworkRigidbody targetNRB, Vector3 force, bool isPush)
{
    // Le serveur est la seule source de vérité pour le cooldown - évite la triche
    if (!_canApplyForce)
        return;

    if (targetNRB != null && targetNRB.PhysicsBody != null)
    {
        targetNRB.PhysicsBody.AddForce(force);
        _canApplyForce = false;
        _timerCooldown = 0f;

        // Propage l'effet visuel + l'animation à TOUS les clients (y compris le lanceur)
        RPC_ShowSpellEffect(isPush);
    }
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ShowSpellEffect(NetworkBool isPush)
{
    effect.ShowBeam(isPush);
    _player.TriggerThrowAnimation();
}
}