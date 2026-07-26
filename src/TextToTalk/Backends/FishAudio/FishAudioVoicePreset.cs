using Newtonsoft.Json;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioVoicePreset : VoicePreset
{
    [JsonProperty("FishAudioVolume")] public float Volume { get; set; }

    public int PlaybackRate { get; set; }

    public string? VoiceId { get; set; }

    public float Temperature { get; set; }

    public float TopP { get; set; }

    public string? ModelId { get; set; }

    public string? Latency { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 100;
        VoiceId = null;
        Temperature = 0.7f;
        TopP = 0.7f;
        ModelId = "s2.1-pro-free";
        Latency = "normal";
        EnabledBackend = TTSBackend.FishAudio;
        return true;
    }
}
