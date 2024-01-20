using System.Text.Json.Serialization;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiVoicePreset : VoicePreset
{
    public float Volume { get; set; }
    
    public string? Model { get; set; }
    
    // 0.25 - 4.0 (default 1.0)
    public float? Speed { get; set; }

    [JsonPropertyName("OpenAIVoiceName")]
    public string? VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        Speed = 1.0f;
        VoiceName = "alloy";
        EnabledBackend = TTSBackend.OpenAi;
        Model = "tts-1";
        return true;
    }
}