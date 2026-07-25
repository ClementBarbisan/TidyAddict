using UnityEngine;

/// <summary>
/// Point de spawn de parchemin, placé/déplacé librement dans l'éditeur.
/// Chaque point porte au plus un parchemin vivant : quand il est consommé
/// par un sort, un nouveau réapparaît ici (mot/sort aléatoire à chaque fois).
/// La scène étant identique chez tous, pas besoin de réseau (le spawn est
/// décidé par le serveur).
/// </summary>
public class ScrollSpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.2f);
    }
}
