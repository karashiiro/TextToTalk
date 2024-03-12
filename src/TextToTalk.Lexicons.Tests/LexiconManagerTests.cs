using System.IO;
using System.Reflection;
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
                .WithLexeme(new Lexeme { Graphemes = new[] { "Bahamut" }, Phoneme = "bɑhɑmɪt", Alias = "Bahamoot"})
                .WithLexeme(new Lexeme { Graphemes = new[] { "Baldesion" }, Phoneme = "bɔldˈɛˈsiɑn" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");
        }

        [Fact]
        public void MakeSsml_Works_WithNoLexicons()
        {
            const string text = "This is some 'text'.";
            var lm = new LexiconManager();
            var ssml = lm.MakeSsml(text, "en-US");
            Assert.True(ssml.Contains(text) && !ssml.Contains("<phoneme"));
        }

        [Fact]
        public void MakeSsml_Works_WithNoReplacements()
        {
            const string text = "This is some 'text'.";
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Bahamut" }, Phoneme = "bɑhɑmɪt", Alias = "Bahamoot" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "Baldesion" }, Phoneme = "bɔldˈɛˈsiɑn" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");
            var ssml = lm.MakeSsml(text, "en-US");
            Assert.True(ssml.Contains(text) && !ssml.Contains("<phoneme"));
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

        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test6()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Y'shtola" }, Phoneme = "jiʃtoʊˈlɑ" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "'s" }, Phoneme = "z" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Y'shtola's", "en-US");
            Assert.True(ssml.Contains("Yshtola") && ssml.Contains("<phoneme ph=\"z\">s</phoneme>"));
        }

        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test7()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Amalj'aa" }, Phoneme = "a.mɔld͡ʒæ" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "Amalj'aas" }, Phoneme = "a.mɔld͡ʒæz" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Amalj'aas", "en-US");
            Assert.True(ssml.Contains("Amaljaas"));
        }

        [Fact]
        public void LongerGraphemes_AreReplacedFirst_Test8()
        {
            var lm = new LexiconManager();
            var lexicon = new LexiconBuilder()
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzean" }, Phoneme = "eɪ ɔrːzɪːæn" })
                .WithLexeme(new Lexeme { Graphemes = new[] { "Eorzea" }, Phoneme = "eɪ ɔrːzɪː ə" })
                .Build();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Eorzean", "en-US");
            Assert.True(ssml.Contains("Eorzean") && ssml.Contains("<phoneme"));
        }

        [Fact]
        public void FullLexicon_VariousReplacementTests()
        {
            using var data = Assembly.GetExecutingAssembly().GetManifestResourceStream("TextToTalk.Lexicons.Tests.test.pls");
            if (data == null) Assert.True(false);

            using var sr = new StreamReader(data);
            var lexicon = sr.ReadToEnd();

            var lm = new LexiconManager();
            var xml = XDocument.Parse(lexicon);
            lm.AddLexicon(xml, "test");

            var ssml = lm.MakeSsml("Alphinaud", "en-GB");
            Assert.True(ssml.Contains("Alphinaud") && ssml.Contains("<phoneme"));

            ssml = lm.MakeSsml("G'raha", "en-GB");
            Assert.True(ssml.Contains("Graha") && ssml.Contains("<phoneme"));

            ssml = lm.MakeSsml("Amalj'aa", "en-GB");
            Assert.True(ssml.Contains("Amaljaa") && ssml.Contains("<phoneme"));
        }

        [Fact]
        public void ReplaceGrapheme_DoesNotDeepReplace()
        {
            var s = LexiconManager.ReplaceGrapheme("Vanu Vanus", "Vanus", "<phoneme>Vanus</phoneme>");
            var t = LexiconManager.ReplaceGrapheme(s, "Vanu", "<phoneme>Vanu</phoneme>");
            Assert.Equal("<phoneme>Vanu</phoneme> <phoneme>Vanus</phoneme>", t);
        }
    }
}
