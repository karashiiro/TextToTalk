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
            
            var ns = xml.Root.Attribute("xmlns")?.Value ?? "";
            var nsPrefix = !string.IsNullOrEmpty(ns) ? $"{{{ns}}}" : "";
            foreach (var lexeme in xml.Root.Descendants($"{nsPrefix}lexeme").Select(el => new
            {
                Graphemes = el.Elements($"{nsPrefix}grapheme").Select(g => g.Value),
                Phoneme = el.Element($"{nsPrefix}phoneme")?.Value,
                Alias = el.Element($"{nsPrefix}alias")?.Value,
            }))
            {
                // https://github.com/karashiiro/TextToTalk/issues/37#issuecomment-899733701
                // There are some weird incompatibilities in the SSML reader that this helps to fix.
                var phoneme = lexeme.Phoneme?
                    .Replace(":", "ː")
                    .Replace(" ", "")
                    .Replace("-", "");

                var graphemes = lexeme.Graphemes.ToList();
                if (!graphemes.Any()) continue;

                foreach (var grapheme in graphemes)
                {
                    if (phoneme != null)
                    {
                        if (lexicon.GraphemePhonemes.ContainsKey(grapheme))
                        {
                            // Allow later graphemes to override previous ones
                            lexicon.GraphemePhonemes.Remove(grapheme);
                        }

                        lexicon.GraphemePhonemes.Add(grapheme, phoneme);
                    }

                    if (lexeme.Alias != null)
                    {
                        if (lexicon.GraphemeAliases.ContainsKey(grapheme))
                        {
                            // Allow later graphemes to override previous ones
                            lexicon.GraphemeAliases.Remove(grapheme);
                        }

                        lexicon.GraphemeAliases.Add(grapheme, lexeme.Alias);
                    }
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
                foreach (var entry in lexicon.GraphemeAliases)
                {
                    var grapheme = entry.Key;
                    var alias = entry.Value;
                    
                    text = text.Replace(grapheme, alias);
                }

                foreach (var entry in lexicon.GraphemePhonemes)
                {
                    var grapheme = entry.Key;
                    var phoneme = entry.Value;

                    // Avoid doing replacements inside of replacements
                    var replacementIndex = text.IndexOf(grapheme, StringComparison.InvariantCulture);
                    var textRight = text[(replacementIndex + grapheme.Length)..];
                    if (StartsWithEndPhonemeTag(textRight)) continue;

                    var phonemeNode = phoneme.Contains("\"")
                        ? $"<phoneme ph='{phoneme}'>{grapheme}</phoneme>"
                        : $"<phoneme ph=\"{phoneme}\">{grapheme}</phoneme>";

                    text = text.Replace(grapheme, phonemeNode);
                }
            }

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{langCode}\">{text}</speak>";
        }
        
        private static bool StartsWithEndPhonemeTag(string text)
        {
            return text.IndexOf("</phoneme", StringComparison.InvariantCultureIgnoreCase) < text.IndexOf("<phoneme", StringComparison.InvariantCultureIgnoreCase);
        }

        private class LexiconInfo
        {
            public string Url { get; set; }

            public IDictionary<string, string> GraphemeAliases { get; } = new ConcurrentDictionary<string, string>();

            public IDictionary<string, string> GraphemePhonemes { get; } = new ConcurrentDictionary<string, string>();
        }
    }
}