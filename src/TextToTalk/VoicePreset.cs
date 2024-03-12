using System;
using Newtonsoft.Json;
using TextToTalk.Backends;

namespace TextToTalk;

// JSON.NET doesn't like if I make this abstract.
public class VoicePreset : IEquatable<VoicePreset>
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

    public bool Equals(VoicePreset? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id && Name == other.Name && ObsoleteRate == other.ObsoleteRate &&
               ObsoleteVolume == other.ObsoleteVolume && ObsoleteVoiceName == other.ObsoleteVoiceName &&
               EnabledBackend == other.EnabledBackend;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == this.GetType() && Equals((VoicePreset)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, ObsoleteRate, ObsoleteVolume, ObsoleteVoiceName, EnabledBackend);
    }
}