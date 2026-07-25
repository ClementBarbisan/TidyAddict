using Fusion;
using UnityEngine;

public class InteractablePhysicsObject : NetworkBehaviour
{
    [SerializeField] private float _floatyGravityScale = 0.3f; // 1 = gravité normale, 0 = apesanteur
    [SerializeField] private float _floatyDrag = 5f;
    [SerializeField] private float _effectDuration = 2f;

    private Rigidbody _rb;
    private float _defaultDrag;

    [Networked] private float _effectTimer { get; set; }
    [Networked] private NetworkBool _isAffected { get; set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _defaultDrag = _rb.linearDamping;
    }

    public override void FixedUpdateNetwork()
    {
        // Seule l'autorité de simulation doit piloter la physique - les proxies
        // reçoivent le résultat via NetworkRigidbody automatiquement.
        if (Object.HasStateAuthority == false)
            return;

        if (_isAffected)
        {
            // Contrebalance une partie de la gravité par défaut du moteur
            _rb.AddForce(Physics.gravity * (_floatyGravityScale - 1f) * _rb.mass, ForceMode.Force);

            _effectTimer -= Runner.DeltaTime;
            if (_effectTimer <= 0f)
            {
                EndEffect();
            }
        }
    }

    /// <summary>
    /// À appeler quand tu ajoutes une force sur l'objet (ex: sort, explosion, poussée).
    /// </summary>
    public void ApplyForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (Object.HasStateAuthority == false)
            return;

        _rb.AddForce(force, mode);
        StartEffect();
    }

    private void StartEffect()
    {
        if (_isAffected == false)
        {
            _rb.linearDamping = _floatyDrag;
        }

        _isAffected = true;
        _effectTimer = _effectDuration; // réinitialise le compte à rebours à chaque interaction
    }

    private void EndEffect()
    {
        _isAffected = false;
        _rb.linearDamping = _defaultDrag;
    }
}