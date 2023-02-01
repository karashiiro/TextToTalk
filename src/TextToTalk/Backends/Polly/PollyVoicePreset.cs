using Amazon.Polly;
using Newtonsoft.Json;

namespace TextToTalk.Backends.Polly;

public class PollyVoicePreset : VoicePreset
{
    [JsonProperty("PollyVolume")] public float Volume { get; set; }

    public int SampleRate { get; set; }

    public int PlaybackRate { get; set; }

    [JsonProperty("PollyVoiceName")] public string? VoiceName { get; set; }

    public string? VoiceEngine { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        SampleRate = 22050;
        PlaybackRate = 100;
        VoiceName = VoiceId.Matthew;
        VoiceEngine = Engine.Neural;
        EnabledBackend = TTSBackend.AmazonPolly;
        return true;
    }
}