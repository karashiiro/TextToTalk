using System.Linq;
using System.Text.Json.Serialization;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiVoicePreset : VoicePreset
{
    public float Volume { get; set; }

    public string? Model { get; set; }

    // 0.25 - 4.0 (default 1.0)
    public float? PlaybackRate { get; set; }

    [JsonPropertyName("OpenAIVoiceName")] public string? VoiceName { get; set; }
    
    public string? Style { get; set; }

    public override bool TrySetDefaultValues()
    {
        var defaultConfig = OpenAiClient.Models.First();
        Volume = 1.0f;
        PlaybackRate = 1.0f;
        VoiceName = defaultConfig.Voices.Keys.First();
        Style = string.Empty;
        EnabledBackend = TTSBackend.OpenAi;
        Model = defaultConfig.ModelName;
        return true;
    }
}