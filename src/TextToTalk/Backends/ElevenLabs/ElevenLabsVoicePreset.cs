using Newtonsoft.Json;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsVoicePreset : VoicePreset
{
    [JsonProperty("ElevenLabsVolume")] public float Volume { get; set; }

    public int PlaybackRate { get; set; }

    public string? VoiceId { get; set; }

    public float SimilarityBoost { get; set; }

    public float Stability { get; set; }

    public string? ModelId { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 100;
        VoiceId = "21m00Tcm4TlvDq8ikWAM";
        SimilarityBoost = 0.5f;
        Stability = 0.5f;
        EnabledBackend = TTSBackend.ElevenLabs;
        return true;
    }
}