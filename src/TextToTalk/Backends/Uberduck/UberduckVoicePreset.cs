using Newtonsoft.Json;

namespace TextToTalk.Backends.Uberduck;

public class UberduckVoicePreset : VoicePreset
{
    [JsonProperty("UberduckVolume")] public float Volume { get; set; }

    public int PlaybackRate { get; set; }

    [JsonProperty("UberduckVoiceName")] public string VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 100;
        VoiceName = "zwf";
        EnabledBackend = TTSBackend.Uberduck;
        return true;
    }
}