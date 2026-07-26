using System;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// Marqueur de zone de collecte : source de vérité de la position ET de la
/// taille de la zone. Génère et maintient lui-même une box mesh translucide
/// aux dimensions exactes du volume de comptage (empreinte × hauteur), dans
/// l'éditeur comme en jeu. Déplacer l'objet déplace la zone ; changer
/// Footprint/Height redimensionne le mesh et le comptage ensemble.
/// </summary>
[ExecuteAlways]
public class CollectionZoneMarker : MonoBehaviour
{
    public Team Team = Team.None;

    [Tooltip("Emprise au sol de la zone (X × Z, en mètres)")]
    [SerializeField] private Vector2 _footprint = new Vector2(8f, 8f);

    [Tooltip("Hauteur de la zone (pour attraper les objets lancés)")]
    [SerializeField] private float _height = 4f;

    [Tooltip("Matériau translucide du mesh (assigné par le setup)")]
    [SerializeField] private Material _visualMaterial;

    private Transform _visual;
    private Transform _ground;

    public Vector3 Center => transform.position + Vector3.up * (_height * 0.5f);
    public Vector3 HalfExtents => new Vector3(_footprint.x * 0.5f, _height * 0.5f, _footprint.y * 0.5f);

    private void Update()
    {
        _visual = EnsureChildBox("Visual", _visual);
        _ground = EnsureChildBox("Ground", _ground);

        // Le volume colle toujours exactement à la zone de comptage
        if (_visual != null)
        {
            _visual.localPosition = new Vector3(0f, _height * 0.5f, 0f);
            _visual.localScale = new Vector3(_footprint.x, _height, _footprint.y);
        }

        // Dalle au sol : reste lisible quand on est à l'intérieur de la zone
        if (_ground != null)
        {
            _ground.localPosition = new Vector3(0f, 0.04f, 0f);
            _ground.localScale = new Vector3(_footprint.x, 0.08f, _footprint.y);
        }
    }

    private Transform EnsureChildBox(string name, Transform cached)
    {
        if (cached != null)
            return cached;

        var child = transform.Find(name);

        if (child == null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(transform, false);

            var collider = box.GetComponent<Collider>();
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);

            child = box.transform;
        }

        if (_visualMaterial != null)
        {
            var renderer = child.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != _visualMaterial)
                renderer.sharedMaterial = _visualMaterial;
        }

        return child;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Grabbable") && other.TryGetComponent<NetworkRigidbody>(out var nrb))
        {
            RPC_ApplyDrag(nrb);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyDrag(NetworkRigidbody targetNRB)
    {
        if (targetNRB != null && targetNRB.PhysicsBody != null)
        {
            targetNRB.PhysicsBody.Drag = 1f;
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Team == Team.Red
            ? new Color(1f, 0.3f, 0.25f, 0.7f)
            : new Color(0.3f, 0.55f, 1f, 0.7f);
        Gizmos.DrawWireCube(Center, HalfExtents * 2f);
    }
}
