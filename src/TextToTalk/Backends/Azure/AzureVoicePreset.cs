using Newtonsoft.Json;

namespace TextToTalk.Backends.Azure;

public class AzureVoicePreset : VoicePreset
{
    [JsonProperty("AzureVolume")] public float Volume { get; set; }

    /// <summary>
    /// The playback rate. Unlike other providers, Azure interprets this as a relative value.
    /// 100% means +100%, or 2x; 20% means +20%, or 1.2x. The documentation says this should
    /// be within 0.5 to 2 times the original audio.
    ///
    /// https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/speech-synthesis-markup-voice#adjust-prosody
    /// </summary>
    public int PlaybackRate { get; set; }

    [JsonProperty("AzureVoiceName")] public string? VoiceName { get; set; }

    public string? Style { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 0;
        VoiceName = "en-US-JennyNeural";
        EnabledBackend = TTSBackend.Azure;
        return true;
    }
}