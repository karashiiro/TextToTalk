using System.Text.Json.Serialization;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudVoicePreset : VoicePreset
{
    public int? SampleRate { get; set; }

    // -20.0 - 20.0 is theoretical max, but it's lowered to work better with sliders (default 0.0)
    public float? Pitch { get; set; }

    public float Volume { get; set; }

    // 0.25 - 4.0 (default 1.0)
    public float? PlaybackRate { get; set; }

    public string? Locale { get; set; }

    public string? Gender { get; set; }

    [JsonPropertyName("GoogleCloudVoiceName")] public string? VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        SampleRate = 22050;
        Pitch = 0.0f;
        Volume = 1.0f;
        PlaybackRate = 1.0f;
        Locale = "en-US";
        VoiceName = "en-US-Wavenet-D";
        Gender = "Male";
        EnabledBackend = TTSBackend.GoogleCloud;
        return true;
    }
}