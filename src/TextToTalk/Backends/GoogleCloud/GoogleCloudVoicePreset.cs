using System.Text.Json.Serialization;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudVoicePreset : VoicePreset
{
    public float Volume { get; set; }
    
    // 0.25 - 4.0 (default 1.0)
    public float? PlaybackRate { get; set; }
    
    public string? Locale { get; set; }

    [JsonPropertyName("GoogleCloudVoiceName")] public string? VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 1.0f;
        Locale = "en-US";
        VoiceName = "en-US-Wavenet-A";
        EnabledBackend = TTSBackend.GoogleCloud;
        return true;
    }
}