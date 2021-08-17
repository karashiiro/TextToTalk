using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TextToTalk.Backends.System
{
    public class LexiconManager
    {
        // The BCL SpeechSynthesizer has an awful lexicon implementation, as described in
        // this SO question https://stackoverflow.com/questions/11529164/how-do-i-use-a-lexicon-with-speechsynthesizer.
        // Because of this, we just manage lexicons ourselves here.

        private readonly IList<LexiconInfo> lexicons;

        public LexiconManager()
        {
            this.lexicons = new List<LexiconInfo>();
        }

        public void AddLexicon(string lexiconUrl)
        {
            var xml = XDocument.Load(lexiconUrl);
            if (xml.Root == null)
            {
                throw new NullReferenceException("XML document has no root element.");
            }

            // Remove existing lexicon if it's already registered
            var existingLexicon = this.lexicons.FirstOrDefault(li => li.Url == lexiconUrl);
            if (existingLexicon != null)
            {
                this.lexicons.Remove(existingLexicon);
            }

            // Create the lexicon
            var lexicon = new LexiconInfo { Url = lexiconUrl };
            this.lexicons.Add(lexicon);

            // Set the lexicon language
            var alphabet = xml.Root.Attribute("alphabet")?.Value ?? lexicon.Alphabet;
            lexicon.Alphabet = alphabet;

            var ns = xml.Root.Attribute("xmlns")?.Value ?? "";
            foreach (var lexeme in xml.Root.Descendants($"{{{ns}}}lexeme").Select(el => new
            {
                Graphemes = el.Elements($"{{{ns}}}grapheme").Select(g => g.Value),
                Phoneme = el.Element($"{{{ns}}}phoneme")?.Value,
            }))
            {
                // https://github.com/karashiiro/TextToTalk/issues/37#issuecomment-899733701
                // There are some weird incompatibilities in the SSML reader that this helps to fix.
                var phoneme = lexeme.Phoneme?
                    .Replace(":", "ː")
                    .Replace(" ", "")
                    .Replace("-", "");
                if (phoneme == null) continue;

                var graphemes = lexeme.Graphemes.ToList();
                if (!graphemes.Any()) continue;

                foreach (var grapheme in graphemes)
                {
                    if (lexicon.GraphemesPhonemes.ContainsKey(grapheme))
                    {
                        // Allow later graphemes to override previous ones
                        lexicon.GraphemesPhonemes.Remove(grapheme);
                    }

                    lexicon.GraphemesPhonemes.Add(grapheme, phoneme);
                }
            }
        }

        public void RemoveLexicon(string lexiconUrl)
        {
            var lexicon = this.lexicons.FirstOrDefault(li => li.Url == lexiconUrl);
            if (lexicon != null)
            {
                this.lexicons.Remove(lexicon);
            }
        }

        public string MakeSsml(string text, string langCode)
        {
            foreach (var lexicon in this.lexicons)
            {
                foreach (var entry in lexicon.GraphemesPhonemes)
                {
                    var grapheme = entry.Key;
                    var phoneme = entry.Value;

                    var phonemeNode = phoneme.Contains("\"")
                        ? $"<phoneme alphabet={lexicon.Alphabet} ph='{phoneme}'>{grapheme}</phoneme>"
                        : $"<phoneme alphabet={lexicon.Alphabet} ph=\"{phoneme}\">{grapheme}</phoneme>";

                    text = text.Replace(grapheme, phonemeNode);
                }
            }

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{langCode}\">{text}</speak>";
        }

        private class LexiconInfo
        {
            public string Url { get; set; }

            public string Alphabet { get; set; } = "ipa";

            public IDictionary<string, string> GraphemesPhonemes { get; } = new ConcurrentDictionary<string, string>();
        }
    }
}