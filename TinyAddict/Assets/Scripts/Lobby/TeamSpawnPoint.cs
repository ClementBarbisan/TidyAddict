using UnityEngine;

/// <summary>
/// Point de spawn d'équipe : au lancement de la partie (fin du compte à
/// rebours), le serveur téléporte chaque gobelin sur un point de son équipe,
/// tourné vers le camp adverse (face à face). Placez/déplacez librement ces
/// points dans l'éditeur — un par joueur et par équipe.
/// </summary>
public class TeamSpawnPoint : MonoBehaviour
{
    public Team Team = Team.None;

    private void OnDrawGizmos()
    {
        Gizmos.color = Team == Team.Red
            ? new Color(1f, 0.3f, 0.25f, 0.9f)
            : new Color(0.3f, 0.55f, 1f, 0.9f);

        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f,
            Team == Team.Red ? "SPAWN R" : "SPAWN B");
#endif
    }
}
