using UnityEngine;

public class EffectPullPush : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Gradient pushColor, pullColor;
    [SerializeField] private Transform spawnSpellTransform;
    [SerializeField] private AudioClip clipPull, clipPush; 
    
    public void ShowBeam(Vector3 end, bool push)
    {
        lineRenderer.SetPosition(0, spawnSpellTransform.position);
        lineRenderer.SetPosition(1, end);
        lineRenderer.colorGradient = push ? pushColor : pullColor;
        lineRenderer.enabled = true;
        Invoke(nameof(ResetLineRenderer), .2f);
    }

    private void ResetLineRenderer()
    {
        lineRenderer.enabled = false;
    }
}
