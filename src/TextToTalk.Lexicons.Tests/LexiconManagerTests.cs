using System.Xml.Linq;
using Xunit;

namespace TextToTalk.Lexicons.Tests
{
    public class LexiconManagerTests
    {
        /// <summary>
        /// Tests that the lexicon builder itself does not cause any exceptions to be thrown.
        /// </summary>
        [Fact]
        public void LexiconBuilder_DoesBuildCorrectly()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new []{ "Bahamut" }, Phoneme = "bɑhɑmɪt", Alias = "Bahamoot"})
                .WithLexeme(new Lexeme { Graphemes = new[] { "Baldesion" }, Phoneme = "bɔldˈɛˈsiɑn" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");
        }

        /// <summary>
        /// Tests that longer graphemes are replaced first when the shorter grapheme comes first in the lexicon.
        /// </summary>
        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test1()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzea" }, Phoneme = "eɪ ɔrːzɪː ə" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzean" }, Phoneme = "eɪ ɔrːzɪːæn" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Eorzean", "en-US");
            Assert.True(ssml.Contains("Eorzean")); // If this is false, the "n" will be cut off and stuck at the end
        }

        /// <summary>
        /// Tests that longer graphemes are replaced first when the shorter grapheme comes last in the lexicon.
        /// </summary>
        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test2()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzean" }, Phoneme = "eɪ ɔrːzɪːæn" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzea" }, Phoneme = "eɪ ɔrːzɪː ə" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Eorzean", "en-US");
            Assert.True(ssml.Contains("Eorzean"));
        }

        /// <summary>
        /// Tests that longer graphemes are replaced first when the graphemes are in a single lexeme and the shorter
        /// grapheme comes first within the lexeme.
        /// </summary>
        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test3()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzea", "Eorzean" }, Phoneme = "eɪ ɔrːzɪː ə" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Eorzean", "en-US");
            Assert.True(ssml.Contains("Eorzean"));
        }

        /// <summary>
        /// Tests that longer graphemes are replaced first when the graphemes are in a single lexeme and the shorter
        /// grapheme comes last within the lexeme.
        /// </summary>
        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test4()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzean", "Eorzea" }, Phoneme = "eɪ ɔrːzɪː ə" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Eorzean", "en-US");
            Assert.True(ssml.Contains("Eorzean"));
        }

        /// <summary>
        /// Tests that longer graphemes are replaced first with respect to an actual input string.
        /// </summary>
        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test5()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Vanu" }, Phoneme = "vɑːnu" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "Vanus" }, Phoneme = "vɑːnuz" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Vanu Vanus", "en-US");
            Assert.True(!ssml.Contains("Vanu Vanus") && ssml.Contains("Vanu") && ssml.Contains("Vanus"));
        }
    }
}
