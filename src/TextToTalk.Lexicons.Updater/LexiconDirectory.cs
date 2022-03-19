using Newtonsoft.Json;

namespace TextToTalk.Lexicons.Updater
{
    public class LexiconDirectory
    {
        [JsonProperty("sha")]
        public string Sha { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("tree")]
        public LexiconDirectoryItem[] Tree { get; set; }
    }
}