using Fusion;
using UnityEngine;

public class TriggerPlayerDetector : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 1. Récupérer le NetworkObject de l'objet qui entre dans le collider
        if (other.CompareTag("Player") && other.TryGetComponent<NetworkObject>(out var networkObj))
        {
            // 2. Extraire le PlayerRef associé à l'autorité d'entrée de cet objet
            PlayerRef player = networkObj.InputAuthority;

            // Vérifier que c'est bien un joueur valide (pas un NPC ou un objet sans joueur)
            if (player != PlayerRef.None)
            {
                Debug.Log($"Le joueur {player.PlayerId} est entré dans la zone !");
                
                // Exemple d'action (à exécuter côté Serveur/Hôte de préférence)
                if (HasStateAuthority)
                {
                    OnPlayerEnteredZone(player, networkObj.GetComponent<NetworkedColor>(), tag);
                }
            }
        }
    }

    private void OnPlayerEnteredZone(PlayerRef player, NetworkedColor color, string tag)
    {
        if (tag == "Blue")
        {
            color.RequestColorChange(Color.blue);
            TeamManager.Instance.SetPlayerTeam(player, Team.Blue);
        }
        else if (tag == "Red")
        {
            color.RequestColorChange(Color.red);
            TeamManager.Instance.SetPlayerTeam(player, Team.Red);
        }
    }
}