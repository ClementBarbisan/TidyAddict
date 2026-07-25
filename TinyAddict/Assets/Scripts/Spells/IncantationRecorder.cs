using System.Collections.Generic;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Capture l'audio sortant du micro Photon (le même flux que le chat vocal,
/// donc les autres joueurs entendent l'incantation) pour le donner à Whisper.
/// À placer sur le GameObject du Recorder (le runner). Branché en pré-processeur,
/// avant la détection de voix, pour capter même les mots dits doucement.
/// </summary>
public class IncantationRecorder : MonoBehaviour
{
    public static IncantationRecorder Instance { get; private set; }

    public Recorder Recorder { get; private set; }
    public int SamplingRate { get; private set; }
    public bool IsCapturing { get; private set; }

    private readonly object _lock = new object();
    private readonly List<float> _buffer = new List<float>(24000 * 8);

    private void Awake()
    {
        Recorder = GetComponent<Recorder>();

        // Le runner de la scène sert de template à FusionBootstrap : le clone actif,
        // créé en dernier, devient l'instance utilisée.
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Appelé par le Recorder Photon via SendMessage quand le flux micro est créé
    private void PhotonVoiceCreated(PhotonVoiceCreatedParams createdParams)
    {
        // Seul le runner réellement connecté crée un flux : c'est lui la bonne instance
        Instance = this;
        SamplingRate = createdParams.Voice.Info.SamplingRate;

        if (createdParams.Voice is LocalVoiceAudioFloat floatVoice)
        {
            floatVoice.AddPreProcessor(new TapFloat(this));
        }
        else if (createdParams.Voice is LocalVoiceAudioShort shortVoice)
        {
            shortVoice.AddPreProcessor(new TapShort(this));
        }
    }

    public void StartCapture()
    {
        lock (_lock)
        {
            _buffer.Clear();
            IsCapturing = true;
        }
    }

    public float[] StopCapture()
    {
        lock (_lock)
        {
            IsCapturing = false;
            float[] samples = _buffer.ToArray();
            _buffer.Clear();
            return samples;
        }
    }

    // Appelés depuis le thread audio de Photon
    private void Append(float[] frame)
    {
        lock (_lock)
        {
            if (IsCapturing)
                _buffer.AddRange(frame);
        }
    }

    private void AppendShort(short[] frame)
    {
        lock (_lock)
        {
            if (IsCapturing == false)
                return;

            for (int i = 0; i < frame.Length; i++)
                _buffer.Add(frame[i] / 32768f);
        }
    }

    private class TapFloat : IProcessor<float>
    {
        private readonly IncantationRecorder _owner;
        public TapFloat(IncantationRecorder owner) => _owner = owner;

        public float[] Process(float[] buf)
        {
            _owner.Append(buf);
            return buf;
        }

        public void Dispose() { }
    }

    private class TapShort : IProcessor<short>
    {
        private readonly IncantationRecorder _owner;
        public TapShort(IncantationRecorder owner) => _owner = owner;

        public short[] Process(short[] buf)
        {
            _owner.AppendShort(buf);
            return buf;
        }

        public void Dispose() { }
    }
}
