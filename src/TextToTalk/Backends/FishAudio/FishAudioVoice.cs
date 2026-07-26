using Newtonsoft.Json;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioVoiceModel
{
    [JsonProperty("_id")] public string? Id { get; init; }

    [JsonProperty("title")] public string? Title { get; init; }

    [JsonProperty("state")] public string? State { get; init; }
}
