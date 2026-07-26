using Fusion.Addons.SimpleKCC;
using UnityEngine;

public class AudioSteps : MonoBehaviour
{
    private SimpleKCC _kcc;
    [SerializeField] private AudioSource _audioSource;

    private void Awake()
    {
        _kcc = GetComponent<SimpleKCC>();
    }

    private void LateUpdate()
    {
        bool isMoving = _kcc.IsGrounded && _kcc.RealVelocity.magnitude > 3f;

        if (isMoving && _audioSource.isPlaying == false)
        {
            _audioSource.Play();
        }
        else if (isMoving == false && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
}
