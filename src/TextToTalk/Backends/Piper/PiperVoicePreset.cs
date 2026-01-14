using Newtonsoft.Json;

namespace TextToTalk.Backends.Piper;

public class PiperVoicePreset : VoicePreset
{
    [JsonProperty("ModelName")]
    public string? InternalName { get; set; }

    [JsonProperty("ModelPath")]
    public string? ModelPath { get; set; }

    public float? Speed { get; set; }

    public float? Volume { get; set; }

    public override bool TrySetDefaultValues()
    {
        InternalName = "en_US-lessac-medium";
        ModelPath = ""; // To be populated by the file picker or downloader
        Speed = 1.0f;
        Volume = 1.0f;
        EnabledBackend = TTSBackend.Piper;
        return true;
    }
}