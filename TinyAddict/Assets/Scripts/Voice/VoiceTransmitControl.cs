using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls when the local microphone transmits.
/// Two modes: push-to-talk (hold a key) or open mic with voice detection.
/// Put this next to the Recorder (on the NetworkRunner GameObject).
/// </summary>
[RequireComponent(typeof(Recorder))]
public class VoiceTransmitControl : MonoBehaviour
{
    public enum Mode
    {
        PushToTalk,
        OpenMicVoiceDetection,
    }

    [SerializeField] private Mode mode = Mode.PushToTalk;

    [Tooltip("Held to transmit in push-to-talk mode.")]
    [SerializeField] private InputAction pushToTalkAction = new(binding: "<Keyboard>/v");

    [Tooltip("Toggles the microphone entirely (works in both modes).")]
    [SerializeField] private InputAction muteToggleAction = new(binding: "<Keyboard>/m");

    private Recorder recorder;
    private bool muted;

    public bool IsTransmitting => recorder != null && recorder.TransmitEnabled && !muted;

    private void Awake()
    {
        recorder = GetComponent<Recorder>();
    }

    private void OnEnable()
    {
        pushToTalkAction.Enable();
        muteToggleAction.Enable();
        muteToggleAction.performed += OnMuteToggle;
        ApplyMode();
    }

    private void OnDisable()
    {
        muteToggleAction.performed -= OnMuteToggle;
        pushToTalkAction.Disable();
        muteToggleAction.Disable();
    }

    private void Update()
    {
        if (mode == Mode.PushToTalk)
        {
            recorder.TransmitEnabled = !muted && pushToTalkAction.IsPressed();
        }
    }

    private void OnMuteToggle(InputAction.CallbackContext _)
    {
        muted = !muted;
        ApplyMode();
    }

    private void ApplyMode()
    {
        switch (mode)
        {
            case Mode.PushToTalk:
                recorder.VoiceDetection = false;
                recorder.TransmitEnabled = false;
                break;
            case Mode.OpenMicVoiceDetection:
                recorder.VoiceDetection = true;
                recorder.TransmitEnabled = !muted;
                break;
        }
    }
}
