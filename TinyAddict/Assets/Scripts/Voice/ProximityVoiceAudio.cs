using UnityEngine;

/// <summary>
/// Configures the AudioSource used by a Photon Voice Speaker for proximity chat:
/// full 3D spatialization with a linear falloff that reaches complete silence
/// at <see cref="maxAudibleDistance"/>.
/// Put this on the same GameObject as the Speaker + AudioSource (player head).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProximityVoiceAudio : MonoBehaviour
{
    [Tooltip("Distance (m) under which the voice is at full volume.")]
    [SerializeField] private float fullVolumeDistance = 2f;

    [Tooltip("Distance (m) beyond which the voice is completely inaudible.")]
    [SerializeField] private float maxAudibleDistance = 18f;

    [Tooltip("Stereo spread in degrees. > 0 softens the hard left/right panning of nearby voices.")]
    [Range(0f, 360f)]
    [SerializeField] private float spread = 60f;

    private void Awake()
    {
        Apply();
    }

    private void OnValidate()
    {
        maxAudibleDistance = Mathf.Max(maxAudibleDistance, fullVolumeDistance + 0.1f);
        Apply();
    }

    private void Apply()
    {
        var source = GetComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = fullVolumeDistance;
        source.maxDistance = maxAudibleDistance;
        source.spread = spread;
        source.dopplerLevel = 0f;
    }
}
