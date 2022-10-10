using Amazon.Polly;

namespace TextToTalk.Backends.Polly;

public class PollyVoicePreset : VoicePreset
{
    public float Rate { get; set; }

    public float Volume { get; set; }

    public int SampleRate { get; set; }
    
    public int PlaybackRate { get; set; }

    public string VoiceName { get; set; }

    public string VoiceEngine { get; set; }

    public override bool TrySetDefaultValues()
    {
        Rate = 1.0f;
        Volume = 1.0f;
        SampleRate = 22050;
        PlaybackRate = 100;
        VoiceName = VoiceId.Matthew;
        VoiceEngine = Engine.Neural;
        return true;
    }
}