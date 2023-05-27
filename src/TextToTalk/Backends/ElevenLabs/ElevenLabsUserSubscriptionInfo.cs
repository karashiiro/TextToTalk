using Newtonsoft.Json;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsUserSubscriptionInfo
{
    [JsonProperty("character_count")] public int CharacterCount { get; init; }

    [JsonProperty("character_limit")] public int CharacterLimit { get; init; }

    [JsonProperty("next_character_count_reset_unix")]
    public int NextCharacterCountResetUnix { get; init; }
}