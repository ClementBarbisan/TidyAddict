using UnityEngine;

public class EffectPullPush : MonoBehaviour
{
    [SerializeField] private ParticleSystem vfxEffectPull, vfxEffectPush;
    [SerializeField] private AudioClip clipPull, clipPush;
    [SerializeField] private AudioSource source;
    
    public void ShowBeam(bool push)
    {
        if (push)
            vfxEffectPush.Play();
        else
            vfxEffectPull.Play();
        
        source.clip = push ? clipPush : clipPull;
        source.Play();
    }
}
