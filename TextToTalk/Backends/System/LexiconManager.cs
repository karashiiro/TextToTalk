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

                    // This is awful and should be done in the earliest preprocessing steps but escaped punctuation doesn't work,
                    // which is the correct way to handle this in SSML.
                    var graphemeReadable = grapheme
                        .Replace("'", "")
                        .Replace("\"", "");

                    var phoneme = entry.Value;
                    
                    var phonemeNode = phoneme.Contains("\"")
                        ? $"<phoneme ph='{phoneme}'>{graphemeReadable}</phoneme>"
                        : $"<phoneme ph=\"{phoneme}\">{graphemeReadable}</phoneme>";

                    text = ReplacePhoneme(text, grapheme, phonemeNode);
                }
            }

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{langCode}\">{text}</speak>";
        }

        private static string ReplacePhoneme(string text, string oldValue, string newValue)
        {
            // Ensure we're not surrounding something that was already surrounded.
            // We build an array in which open tags (<phoneme>) are represented by 1,
            // and and close tags (</phoneme>) are represented by 2.
            var curMarker = (byte)1;
            var tags = new byte[text.Length];
            var inTag = false;
            var lastCharWasLeftBracket = false;
            for (var i = 0; i < text.Length; i++)
            {
                if (lastCharWasLeftBracket)
                {
                    lastCharWasLeftBracket = false;
                    curMarker = text[i] == '/' ? (byte)2 : (byte)1;
                    tags[i - 1] = curMarker;
                }

                if (inTag)
                {
                    tags[i] = curMarker;
                }

                if (!inTag && text[i] == '<')
                {
                    inTag = true;
                    lastCharWasLeftBracket = true;
                    tags[i] = curMarker;
                }

                if (text[i] == '>')
                {
                    inTag = false;
                    tags[i] = curMarker;
                }
            }

            // Starting from the index of the text we want to replace, we move right
            // and ensure that we do not encounter a 2 before we encounter a 1.
            var xIdx = text.IndexOf(oldValue, StringComparison.InvariantCulture);
            for (var i = xIdx; i < text.Length; i++)
            {
                if (tags[i] == 1)
                {
                    // A-OK
                    break;
                }

                if (tags[i] == 2)
                {
                    // Not A-OK, return early
                    return text;
                }
            }

            return text.Replace(oldValue, newValue);
        }

        private class LexiconInfo
        {
            public string Url { get; set; }

            public IDictionary<string, string> GraphemeAliases { get; } = new ConcurrentDictionary<string, string>();

            public IDictionary<string, string> GraphemePhonemes { get; } = new ConcurrentDictionary<string, string>();
        }
    }
}