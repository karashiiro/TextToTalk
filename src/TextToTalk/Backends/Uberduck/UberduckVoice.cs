using Newtonsoft.Json;

namespace TextToTalk.Backends.Uberduck;

public class UberduckVoice
{
    [JsonProperty("display_name")] public required string DisplayName { get; init; }

    [JsonProperty("name")] public required string Name { get; init; }

    [JsonProperty("voicemodel_uuid")] public required string VoiceModelUuid { get; init; }

    [JsonProperty("category")] public required string Category { get; init; }
}