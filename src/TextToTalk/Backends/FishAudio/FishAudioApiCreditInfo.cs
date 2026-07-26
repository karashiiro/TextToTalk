using Newtonsoft.Json;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioApiCreditInfo
{
    [JsonProperty("credit")] public double Credit { get; init; }
}
