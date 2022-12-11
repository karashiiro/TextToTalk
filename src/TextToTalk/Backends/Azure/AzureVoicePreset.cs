using Newtonsoft.Json;

namespace TextToTalk.Backends.Azure;

public class AzureVoicePreset : VoicePreset
{
    [JsonProperty("AzureVolume")] public float Volume { get; set; }

    public int PlaybackRate { get; set; }

    [JsonProperty("AzureVoiceName")] public string VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 100;
        VoiceName = "en-US-JennyNeural";
        EnabledBackend = TTSBackend.Azure;
        return true;
    }
}