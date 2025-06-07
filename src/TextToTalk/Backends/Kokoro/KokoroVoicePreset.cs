using System.Text.Json.Serialization;

namespace TextToTalk.Backends.Kokoro;

public class KokoroVoicePreset : VoicePreset
{
    [JsonPropertyName("KokoroInternalName")]
    public string? InternalName { get; set; }
    public float? Speed { get; set; }

    public override bool TrySetDefaultValues()
    {
        InternalName = "af_heart";
        Speed = 1f;
        EnabledBackend = TTSBackend.Kokoro;
        return true;
    }
}