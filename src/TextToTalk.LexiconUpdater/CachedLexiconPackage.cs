using System.Collections.Generic;

namespace TextToTalk.LexiconUpdater
{
    internal class CachedLexiconPackage
    {
        public IDictionary<string, string> FileETags { get; }

        public CachedLexiconPackage()
        {
            FileETags = new Dictionary<string, string>();
        }
    }
}