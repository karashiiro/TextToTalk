using System.Linq;
using System.Speech.Synthesis;

namespace TextToTalk.Backends.System;

public class SystemVoicePreset : VoicePreset
{
    public int Rate { get; set; }

    public int Volume { get; set; }

    public string VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
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