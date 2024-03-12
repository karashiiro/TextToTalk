using System;
using TextToTalk.Utils;
using Xunit;

namespace TextToTalk.Tests.Utils;

public class TalkUtilsTests
{
    [Theory]
    [InlineData("...")]
    public void IsSpeakable_WithUnspeakableText_ReturnsFalse(string input)
    {
        var actual = TalkUtils.IsSpeakable(input);
        Assert.False(actual);
    }

    [Theory]
    [InlineData("Well...")]
    [InlineData("this is some speakable text")]
    public void IsSpeakable_WithSpeakableText_ReturnsTrue(string input)
    {
        var actual = TalkUtils.IsSpeakable(input);
        Assert.True(actual);
    }

    [Theory]
    [InlineData("Something & something else", "Something and something else")]
    public void ReplaceSsmlTokens_ReplacesKnownTokens(string input, string expected)
    {
        var actual = TalkUtils.ReplaceSsmlTokens(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("—", " - ")]
    public void NormalizePunctuation_ReplacesHyphens(string input, string expected)
    {
        var actual = TalkUtils.NormalizePunctuation(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("<sigh> Forgive me my outburst.", "Forgive me my outburst.")]
    public void StripAngleBracketedText_RemovesAngleBracketedText(string input, string expected)
    {
        var actual = TalkUtils.StripAngleBracketedText(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("あ-あ", "あ")]
    [InlineData("b-but", "but")]
    [InlineData("H-H-Hello!", "Hello!")]
    [InlineData("So th-there w-was", "So there was")]
    public void RemoveStutters_RemovesRepeatedHyphenatedLetters(string input, string expected) =>
        TestRemoveStutters(input, expected);

    [Theory]
    [InlineData("Th-this has different c-capitalization", "This has different capitalization")]
    [InlineData("É-é-é", "É")]
    public void RemoveStutters_MaintainsCapitalization(string input, string expected) =>
        TestRemoveStutters(input, expected);

    [Theory]
    [InlineData("A-Ruhn?", "A-Ruhn?")]
    public void RemoveStutters_DoesNotRemoveDifferentHyphenatedLetters(string input, string expected) =>
        TestRemoveStutters(input, expected);

    [Theory]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData("a-a-", "a-")]
    [InlineData("お礼がてら、あなたもいかがかしら？", "お礼がてら、あなたもいかがかしら？")] // quest/006/TstPln905_00659
    public void RemoveStutters_EdgeCasesWork(string input, string expected) => TestRemoveStutters(input, expected);

    [Theory]
    [InlineData(null, FirstOrLastName.First, null)]
    [InlineData("", FirstOrLastName.First, null)]
    [InlineData(" ", FirstOrLastName.First, null)]
    [InlineData("Two Names", FirstOrLastName.First, "Two")]
    [InlineData("Two Names", FirstOrLastName.Last, "Names")]
    [InlineData("Maybe Three Names", FirstOrLastName.Last, "Names")]
    [InlineData("Onename", FirstOrLastName.First, "Onename")]
    [InlineData("Onename", FirstOrLastName.Last, "Onename")]
    public void GetPartialName_CanExtractNamePart(string? input, FirstOrLastName part, string? expected)
    {
        var actual = TalkUtils.GetPartialName(input, part);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetPartialName_WithInvalidEnumValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TalkUtils.GetPartialName("Someone", (FirstOrLastName)(-1)));
    }

    private static void TestRemoveStutters(string input, string expected)
    {
        var actual = TalkUtils.RemoveStutters(input);
        Assert.Equal(expected, actual);
    }
}