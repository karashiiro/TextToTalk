using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Policy;
using System.Xml;

namespace TextToTalk.Backends.System
{
    public class LexiconManager
    {
        // The BCL SpeechSynthesizer has an awful lexicon implementation, as described in
        // this SO question https://stackoverflow.com/questions/11529164/how-do-i-use-a-lexicon-with-speechsynthesizer.
        // Because of this, we just manage lexicons ourselves here.

        private readonly IDictionary<string, IDictionary<string, string>> lexicons;

        public LexiconManager()
        {
            this.lexicons = new ConcurrentDictionary<string, IDictionary<string, string>>();
        }

        public void AddLexicon(string lexiconUrl)
        {
            var doc = new XmlDocument();
            doc.Load(lexiconUrl);

            var root = doc.DocumentElement ?? throw new NullReferenceException("Document has no root element.");
            var lexemes = root.SelectNodes("/lexeme");

            var replacements = new ConcurrentDictionary<string, string>();
            this.lexicons.Add(lexiconUrl, replacements);
            if (lexemes == null) return;

            foreach (XmlNode node in lexemes)
            {
                var phoneme = node.SelectSingleNode("/phoneme");
                if (phoneme == null) continue;

                var graphemes = node.SelectNodes("/grapheme");
                if (graphemes == null) continue;

                foreach (XmlNode grapheme in graphemes)
                {
                    this.lexicons[lexiconUrl].Add(grapheme.InnerText, phoneme.InnerText);
                }
            }
        }

        public void RemoveLexicon(string lexiconUrl)
        {
            this.lexicons.Remove(lexiconUrl);
        }

        public string MakeSsml(string text)
        {
            foreach (var lexicon in this.lexicons.Values)
            {
                foreach (var entry in lexicon)
                {
                    var grapheme = entry.Key;
                    var phoneme = entry.Value;

                    text = text.Replace(grapheme, $"<phoneme ph=\"{phoneme}\">{grapheme}</phoneme>");
                }
            }

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{text}</speak>";
        }
    }
}