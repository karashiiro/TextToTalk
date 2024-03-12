using TextToTalk.Utils;
using Xunit;

namespace TextToTalk.Tests.Utils;

public class TalkUtilsTests
{
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

    private static void TestRemoveStutters(string input, string expected)
    {
        var actual = TalkUtils.RemoveStutters(input);
        Assert.Equal(expected, actual);
    }
}