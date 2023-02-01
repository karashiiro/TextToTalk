using System.Linq;
using System.Speech.Synthesis;
using Newtonsoft.Json;

namespace TextToTalk.Backends.System;

public class SystemVoicePreset : VoicePreset
{
    [JsonProperty("SystemRate")] public int Rate { get; set; }

    [JsonProperty("SystemVolume")] public int Volume { get; set; }

    [JsonProperty("SystemVoiceName")] public string? VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        EnabledBackend = TTSBackend.System;
        
        using var ss = new SpeechSynthesizer();
        var defaultVoiceInfo = ss.GetInstalledVoices().FirstOrDefault();
        if (defaultVoiceInfo != null)
        {
            Rate = ss.Rate;
            Volume = ss.Volume;
            VoiceName = defaultVoiceInfo.VoiceInfo.Name;
            return true;
        }

        VoiceName = string.Empty;

        return false;
    }
}