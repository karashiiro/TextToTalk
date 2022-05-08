using System;
using System.Collections.Generic;
using System.IO;
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

        public int Entries => this.lexicons.SelectMany(l => l.Lexemes).Count();

        public LexiconManager()
        {
            this.lexicons = new List<LexiconInfo>();
        }

        public virtual int AddLexicon(Stream data, string lexiconId)
        {
            var xml = XDocument.Load(data);
            return AddLexicon(xml, lexiconId);
        }

        public virtual int AddLexicon(string lexiconUrl)
        {
            var xml = XDocument.Load(lexiconUrl);
            return AddLexicon(xml, lexiconUrl);
        }

        internal int AddLexicon(XDocument xml, string lexiconId)
        {
            if (xml.Root == null)
            {
                throw new NullReferenceException("XML document has no root element.");
            }

            // Remove existing lexicon if it's already registered
            var existingLexicon = this.lexicons.FirstOrDefault(li => li.Id == lexiconId);
            if (existingLexicon != null)
            {
                this.lexicons.Remove(existingLexicon);
            }

            var ns = xml.Root.Attribute("xmlns")?.Value ?? "";
            var nsPrefix = !string.IsNullOrEmpty(ns) ? $"{{{ns}}}" : "";

            // Create the lexicon
            var lexicon = new LexiconInfo
            {
                Id = lexiconId,
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
                                .Replace("-", "")
                                .Replace("ʤ", "d͡ʒ"),
                            Alias = el.Element($"{nsPrefix}alias")?.Value,
                        })
                    )
                    .Where(lexeme => !string.IsNullOrEmpty(lexeme.Phoneme))
                    .ToList(),
            };

            lexicon.Lexemes.Sort((a, b) => b.Grapheme.Length - a.Grapheme.Length);

            this.lexicons.Add(lexicon);

            return lexicon.Lexemes.Count;
        }

        public virtual void RemoveLexicon(string lexiconId)
        {
            var lexicon = this.lexicons.FirstOrDefault(li => li.Id == lexiconId);
            if (lexicon != null)
            {
                this.lexicons.Remove(lexicon);
            }
        }

        public virtual string MakeSsml(string text, string langCode = null, int playbackRate = -1, bool includeSpeakAttributes = true)
        {
            foreach (var lexicon in this.lexicons)
            {
                foreach (var lexeme in lexicon.Lexemes.Where(lexeme => text.Contains(lexeme.Grapheme) || (!string.IsNullOrEmpty(lexeme.Alias) && text.Contains(lexeme.Alias))))
                {
                    text = WrapGrapheme(text, lexeme.Grapheme, lexeme.Phoneme);
                    if (!string.IsNullOrEmpty(lexeme.Alias))
                    {
                        text = WrapGrapheme(text, lexeme.Alias, lexeme.Phoneme);
                    }
                }
            }

            if (playbackRate >= 0)
            {
                text = $"<prosody rate=\"{playbackRate}%\">{text}</prosody>";
            }

            // Generate speak tag
            var speakTag = "<speak";
            if (includeSpeakAttributes)
            {
                speakTag += " version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\"";

                if (langCode != null)
                {
                    speakTag += $" xml:lang=\"{langCode}\"";
                }
            }

            speakTag += ">";

            return $"{speakTag}{text}</speak>";
        }

        private static string WrapGrapheme(string text, string grapheme, string phoneme)
        {
            // Escaped punctuation doesn't work with System.Speech.
            var graphemeReadable = grapheme
                .Replace("'", "")
                .Replace("\"", "");

            var phonemeNode = phoneme.Contains("\"")
                ? $"<phoneme ph='{phoneme}'>{graphemeReadable}</phoneme>"
                : $"<phoneme ph=\"{phoneme}\">{graphemeReadable}</phoneme>";

            return ReplaceGrapheme(text, grapheme, phonemeNode);
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
            public string Id { get; init; }

            public List<MicroLexeme> Lexemes { get; init; }
        }
    }
}