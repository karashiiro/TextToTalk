using Newtonsoft.Json;
using System.Collections.Generic;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsVoice
{
    [JsonProperty("voice_id")] public string? VoiceId { get; init; }

    [JsonProperty("name")] public string? Name { get; init; }

    [JsonProperty("category")] public string? Category { get; init; }
}

public class ElevenLabsModel
{
    [JsonProperty("model_id")] public string? ModelId { get; init; }

    [JsonProperty("description")] public string? ModelDescription { get; init; }

    [JsonProperty("can_do_text_to_speech")] public bool CanDoTts { get; init; }

    [JsonProperty("model_rates")] public Dictionary<string, double>? ModelRates { get; init; }
}