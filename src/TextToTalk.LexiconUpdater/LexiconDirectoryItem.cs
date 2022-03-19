using Newtonsoft.Json;

namespace TextToTalk.LexiconUpdater
{
    public class LexiconDirectoryItem
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("sha")]
        public string Sha { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}