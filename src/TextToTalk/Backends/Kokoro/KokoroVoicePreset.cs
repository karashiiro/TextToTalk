using Newtonsoft.Json;

namespace TextToTalk.Backends.Kokoro;

public class KokoroVoicePreset : VoicePreset
{
    [JsonProperty("KokoroInternalName")]
    public string? InternalName { get; set; }
    public float? Speed { get; set; }
    public float? Volume { get; set; }

    public override bool TrySetDefaultValues()
    {
        InternalName = "af_heart";
        Speed = 1f;
        EnabledBackend = TTSBackend.Kokoro;
        Volume = 0.6f;
        return true;
    }
}