using System.IO;
using System.Reflection;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using TextToTalk.Lexicons;
using Xunit;
using Xunit.Abstractions;

namespace TextToTalk.Tests.Backends.System;

public class SystemLexiconTests(ITestOutputHelper output)
{
    [Fact]
    public void SpeakSsml_DoesNotThrow()
    {
        var lexiconManager = new LexiconManager();
        var speechSynthesizer = new SpeechSynthesizer();
        var ssml = lexiconManager.MakeSsml("Hello, world!", langCode: speechSynthesizer.Voice.Culture.IetfLanguageTag);
        speechSynthesizer.SpeakSsml(ssml);
    }

    [Fact]
    public async Task SpeakSsml_CharactersLocationsSystemLexicon_DoesNotThrow()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var lexiconPath = Path.Join(Path.GetDirectoryName(assemblyLocation),
            "../../../../../lexicons/Characters-Locations-System/lexicon.pls");
        await using var lexicon = File.Open(lexiconPath, FileMode.Open);

        var lexiconManager = new LexiconManager();
        lexiconManager.AddLexicon(lexicon, "Characters-Locations-System");

        var speechSynthesizer = new SpeechSynthesizer();
        foreach (var lexeme in lexiconManager.Lexemes("Characters-Locations-System"))
        {
            output.WriteLine($"Testing grapheme {lexeme.Grapheme} ({lexeme.Phoneme})");

            var ssml = lexiconManager.MakeSsml(lexeme.Grapheme, langCode: speechSynthesizer.Voice.Culture.IetfLanguageTag);
            speechSynthesizer.SpeakSsml(ssml);
        }
    }
}