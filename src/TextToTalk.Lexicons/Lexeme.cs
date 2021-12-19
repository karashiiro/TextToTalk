using System.Collections.Generic;

namespace TextToTalk.Lexicons
{
    public class Lexeme
    {
        public IEnumerable<string> Graphemes { get; init; }

        public string Phoneme { get; init; }

        public string Alias { get; init; }
    }
}