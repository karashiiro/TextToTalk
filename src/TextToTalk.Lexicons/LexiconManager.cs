using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TextToTalk.Lexicons
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
            AddLexicon(xml, lexiconUrl);
        }

        public void AddLexicon(XDocument xml, string lexiconUrl)
        {
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

            var ns = xml.Root.Attribute("xmlns")?.Value ?? "";
            var nsPrefix = !string.IsNullOrEmpty(ns) ? $"{{{ns}}}" : "";

            // Create the lexicon
            var lexicon = new LexiconInfo
            {
                Url = lexiconUrl,
                Lexemes = xml.Root.Descendants($"{nsPrefix}lexeme")
                    .SelectMany(el => el.Elements($"{nsPrefix}grapheme")
                        .Select(grapheme => new MicroLexeme
                        {
                            Grapheme = grapheme.Value,
                            // https://github.com/karashiiro/TextToTalk/issues/37#issuecomment-899733701
                            // There are some weird incompatibilities in the SSML reader that this helps to fix.
                            Phoneme = el.Element($"{nsPrefix}phoneme")?.Value
                                .Replace(":", "ː")
                                .Replace(" ", "")
                                .Replace("-", ""),
                            Alias = el.Element($"{nsPrefix}alias")?.Value,
                        })
                    )
                    .Where(lexeme => !string.IsNullOrEmpty(lexeme.Phoneme))
                    .ToList(),
            };

            lexicon.Lexemes.Sort((a, b) => b.Grapheme.Length - a.Grapheme.Length);

            this.lexicons.Add(lexicon);
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
                foreach (var lexeme in lexicon.Lexemes.Where(lexeme => text.Contains(lexeme.Grapheme)))
                {
                    if (!string.IsNullOrEmpty(lexeme.Alias))
                    {
                        text = text.Replace(lexeme.Grapheme, lexeme.Alias);
                    }

                    // This is awful and should be done in the earliest preprocessing steps but escaped punctuation doesn't work
                    // with System.Speech, which would be correct way to handle this.
                    var graphemeReadable = lexeme.Grapheme
                        .Replace("'", "")
                        .Replace("\"", "");

                    var phonemeNode = lexeme.Phoneme.Contains("\"")
                        ? $"<phoneme ph='{lexeme.Phoneme}'>{graphemeReadable}</phoneme>"
                        : $"<phoneme ph=\"{lexeme.Phoneme}\">{graphemeReadable}</phoneme>";

                    text = ReplaceGrapheme(text, lexeme.Grapheme, phonemeNode);
                }
            }

            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{langCode}\">{text}</speak>";
        }

        internal static string ReplaceGrapheme(string text, string oldValue, string newValue)
        {
            var xIdx = text.IndexOf(oldValue, StringComparison.InvariantCulture);
            if (xIdx == -1)
            {
                return text;
            }

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

            return text[..xIdx] + newValue + ReplaceGrapheme(text[(xIdx + oldValue.Length)..], oldValue, newValue);
        }

        private class MicroLexeme
        {
            public string Grapheme { get; init; }

            public string Phoneme { get; init; }

            public string Alias { get; init; }
        }

        private class LexiconInfo
        {
            public string Url { get; init; }

            public List<MicroLexeme> Lexemes { get; init; }
        }
    }
}