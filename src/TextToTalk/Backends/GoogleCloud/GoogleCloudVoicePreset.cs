using System.Text.Json.Serialization;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudVoicePreset : VoicePreset
{
    public float Volume { get; set; }

    // 0.25 - 2.0 (default 1.0)
    public float? PlaybackRate { get; set; }

    public string? Locale { get; set; }

    public string? Gender { get; set; }

    [JsonPropertyName("GoogleCloudVoiceName")] public string? VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 1.0f;
        Locale = "en-US";
        VoiceName = "en-US-Chirp-HD-D";
        Gender = "Male";
        EnabledBackend = TTSBackend.GoogleCloud;
        return true;
    }
}