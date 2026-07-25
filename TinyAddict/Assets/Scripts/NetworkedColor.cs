using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class NetworkedColor : NetworkBehaviour
{
    // Propriété réseau synchronisée automatiquement
    [Networked]
    public Color ObjectColor { get; set; }

    private ChangeDetector _changes;
    [SerializeField] private List<Renderer> _renderers = new List<Renderer>();

    public override void Spawned()
    {
        if (_renderers.Count == 0)
            _renderers = GetComponentsInChildren<Renderer>().ToList();
        // Initialisation du détecteur de changements
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        // Appliquer la couleur initiale au démarrage
        ApplyColor(ObjectColor);
    }

    public override void Render()
    {
        // On vérifie à chaque frame visuelle si une variable réseau a changé
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(ObjectColor))
            {
                ApplyColor(ObjectColor);
            }
        }
    }

    private void ApplyColor(Color newColor)
    {
        if (_renderers != null && _renderers.Count > 0)
        {
            foreach (Renderer render in _renderers)
            {
                render.material.color = newColor;
            }
        }
    }

    // Méthode pour demander le changement de couleur (à appeler depuis le client ou le serveur)
    public void RequestColorChange(Color newColor)
    {
        if (HasStateAuthority)
        {
            // Si on a l'autorité d'état (ex: l'Hôte ou le Serveur), on modifie directement
            ObjectColor = newColor;
        }
        else
        {
            // Sinon, on demande au serveur via un RPC de changer la couleur
            RPC_RequestColorChange(newColor);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestColorChange(Color newColor)
    {
        ObjectColor = newColor;
    }
}