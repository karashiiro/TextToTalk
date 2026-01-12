using Newtonsoft.Json;
using System.Collections.Generic;

namespace TextToTalk.Backends.Uberduck;

public class UberduckVoice
{
    [JsonProperty("display_name")]
    public required string DisplayName { get; init; }

    [JsonProperty("name")]
    public required string Name { get; init; }

    [JsonProperty("voicemodel_uuid")]
    public required string VoiceModelUuid { get; init; }

    [JsonProperty("category")]
    public string? Category { get; init; }

    // New for 2026: Useful for filtering by locale
    [JsonProperty("language")]
    public string? Language { get; init; }

    // New for 2026: Contains traits like "professional", "narrative", etc.
    [JsonProperty("tags")]
    public List<string> Tags { get; init; } = new();
}

public class UberduckVoiceResponse
{
    [JsonProperty("voices")]
    public IList<UberduckVoice> Voices { get; set; }
}