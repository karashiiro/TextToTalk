using Dalamud.Plugin;
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

        private readonly IDictionary<string, IDictionary<string, string>> lexicons;

        public LexiconManager()
        {
            this.lexicons = new ConcurrentDictionary<string, IDictionary<string, string>>();
        }

        public void AddLexicon(string lexiconUrl)
        {
            var xml = XDocument.Load(lexiconUrl);
            if (xml.Root == null)
            {
                throw new NullReferenceException("XML document has no root element.");
            }

            var replacements = new ConcurrentDictionary<string, string>();

            if (this.lexicons.ContainsKey(lexiconUrl))
            {
                this.lexicons.Remove(lexiconUrl);
            }

            this.lexicons.Add(lexiconUrl, replacements);

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
                    if (this.lexicons[lexiconUrl].ContainsKey(grapheme))
                    {
                        // Allow later graphemes to override previous ones
                        this.lexicons[lexiconUrl].Remove(grapheme);
                    }
                    
                    this.lexicons[lexiconUrl].Add(grapheme, phoneme);
                }
            }
        }

        public void RemoveLexicon(string lexiconUrl)
        {
            this.lexicons.Remove(lexiconUrl);
        }

        public string MakeSsml(string text, string langCode)
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
            
            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{langCode}\">{text}</speak>";
        }
    }
}