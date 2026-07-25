using Fusion.Addons.SimpleKCC;
using UnityEngine;

public class AudioSteps : MonoBehaviour
{
    private SimpleKCC _kcc;
    [SerializeField] private AudioSource audio;
    private void Awake()
    {
        _kcc = GetComponent<SimpleKCC>();
    }
    void LateUpdate()
    {
        if (_kcc.IsGrounded && _kcc.RealVelocity.magnitude > 3f && !audio.isPlaying)
        {
            audio.Play();
        }
        else if(audio.isPlaying)
            audio.Stop();
    }
}
