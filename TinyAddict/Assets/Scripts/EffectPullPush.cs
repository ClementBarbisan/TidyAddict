using UnityEngine;

public class EffectPullPush : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform spawnSpellTransform;
    [SerializeField] private AudioClip clipPull, clipPush;
    [SerializeField] private AudioSource source;

    private Material _mat;

    private void Awake()
    {
        _mat = GetComponent<Renderer>().material;
    }
    
    public void ShowBeam(Vector3 end, bool push)
    {
        lineRenderer.SetPosition(0, spawnSpellTransform.position);
        lineRenderer.SetPosition(1, end);
        _mat.color = push ? Color.green : Color.red;
        lineRenderer.enabled = true;
        source.clip = push ? clipPush : clipPull;
        source.Play();
        Invoke(nameof(ResetLineRenderer), .2f);
    }

    private void ResetLineRenderer()
    {
        lineRenderer.enabled = false;
    }
}
