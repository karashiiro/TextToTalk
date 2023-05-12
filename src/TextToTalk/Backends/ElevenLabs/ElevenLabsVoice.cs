using Newtonsoft.Json;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsVoice
{
    [JsonProperty("voice_id")] public string? VoiceId { get; init; }

    [JsonProperty("name")] public string? Name { get; init; }

    [JsonProperty("category")] public string? Category { get; init; }
}