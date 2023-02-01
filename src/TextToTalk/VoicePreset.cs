using System;
using Newtonsoft.Json;
using TextToTalk.Backends;

namespace TextToTalk;

// JSON.NET doesn't like if I make this abstract.
public class VoicePreset
{
    public int Id { get; set; }

    public string? Name { get; set; }

    [JsonProperty("Rate")]
    [Obsolete("This class used to be used for System voice presets. Use SystemVoicePreset instead.")]
    public int ObsoleteRate { get; set; }

    [JsonProperty("Volume")]
    [Obsolete("This class used to be used for System voice presets. Use SystemVoicePreset instead.")]
    public int ObsoleteVolume { get; set; }

    [JsonProperty("VoiceName")]
    [Obsolete("This class used to be used for System voice presets. Use SystemVoicePreset instead.")]
    public string? ObsoleteVoiceName { get; set; }

    public TTSBackend EnabledBackend { get; set; }

    public virtual bool TrySetDefaultValues()
    {
        return true;
    }
}