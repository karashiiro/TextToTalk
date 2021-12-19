using System.Xml.Linq;
using Xunit;

namespace TextToTalk.Lexicons.Tests
{
    public class LexiconManagerTests
    {
        [Fact]
        public void LexiconBuilder_DoesBuild()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new []{ "Bahamut" }, Phoneme = "bɑhɑmɪt", Alias = "Bahamoot"})
                .WithLexeme(new Lexeme { Graphemes = new[] { "Baldesion" }, Phoneme = "bɔldˈɛˈsiɑn" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");
        }
    }
}
