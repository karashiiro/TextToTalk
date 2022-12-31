using Newtonsoft.Json;

namespace TextToTalk.Backends.Uberduck;

public class UberduckVoice
{
    [JsonProperty("display_name")]
    public string DisplayName { get; init; }

    [JsonProperty("name")]
    public string Name { get; init; }
    
    [JsonProperty("category")]
    public string Category { get; init; }
}