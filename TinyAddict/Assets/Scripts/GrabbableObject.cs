using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GrabbableObject : NetworkBehaviour
{
    // Synchronise le joueur qui tient actuellement l'objet sur le réseau
    [Networked] public PlayerRef CurrentHolder { get; set; }
    
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Appelé par le joueur quand il veut attraper l'objet
    public void Grab(PlayerRef player, Transform attachPoint)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHolder = player;
        
        // Transfère l'autorité d'entrée au joueur qui prend l'objet
        Object.AssignInputAuthority(player);

        // Désactive la physique pendant le portage
        if (_rb != null)
        {
            _rb.isKinematic = true;
        }

        // Parent l'objet au point d'attache du joueur (ex: sa main)
        transform.SetParent(attachPoint, true);
        //transform.localPosition = Vector3.zero;
        //transform.localRotation = Quaternion.identity;
    }

    // Appelé par le joueur quand il veut relâcher l'objet
    public void Release(Vector3 throwForce = default)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHolder = PlayerRef.None;
        
        // Retire l'Input Authority
        Object.RemoveInputAuthority();

        // Déparente l'objet
        transform.SetParent(null);

        // Réactive la physique et applique éventuellement une force d'envoi
        if (_rb != null)
        {
            _rb.isKinematic = false;
            if (throwForce != Vector3.zero)
            {
                _rb.AddForce(throwForce, ForceMode.VelocityChange);
            }
        }
    }
}