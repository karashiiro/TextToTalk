using System.Collections.Generic;

namespace TextToTalk.Lexicons.Updater
{
    internal class CachedLexicon
    {
        public IDictionary<string, string> FileETags { get; }

        public CachedLexicon()
        {
            FileETags = new Dictionary<string, string>();
        }
    }
}