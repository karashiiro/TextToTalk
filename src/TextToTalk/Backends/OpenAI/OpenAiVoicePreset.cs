using System.Collections.Generic;
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

    public string? Style { get; set; } = "";

    [JsonIgnore] public SortedSet<string> Styles { get; set; } = new SortedSet<string>();


    public void SyncSetFromString()
    {
        Styles.Clear();
        if (string.IsNullOrWhiteSpace(Style)) return;

        foreach (var s in Style.Split(", "))
            Styles.Add(s);
    }

    // Call this whenever the UI changes the Set to update the String
    public void SyncStringFromSet()
    {
        Style = string.Join(", ", Styles);
    }

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