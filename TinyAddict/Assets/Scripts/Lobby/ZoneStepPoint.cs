using UnityEngine;

/// <summary>
/// Étape du parcours d'une zone de collecte : la zone de l'équipe saute sur
/// le point de l'étape courante (le match est découpé en autant de tranches
/// que d'étapes — 3 par défaut : départ + 2 changements). Placez un point par
/// étape et par équipe dans la scène et déplacez-les librement.
/// La scène étant identique chez tous, chaque client déplace localement les
/// zones au même moment (calé sur le chrono réseau).
/// </summary>
public class ZoneStepPoint : MonoBehaviour
{
    public Team Team = Team.None;

    [Tooltip("Numéro d'étape : 0 = position de départ, puis 1, 2… à chaque changement")]
    public int Step;

    private void OnDrawGizmos()
    {
        Gizmos.color = Team == Team.Red
            ? new Color(1f, 0.3f, 0.25f, 0.8f)
            : new Color(0.3f, 0.55f, 1f, 0.8f);

        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(2f, 0.2f, 2f));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f,
            $"{(Team == Team.Red ? "R" : "B")}{Step + 1}");
#endif
    }
}
