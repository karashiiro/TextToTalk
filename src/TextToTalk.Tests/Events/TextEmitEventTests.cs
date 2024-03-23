using Dalamud.Game.Text;
using TextToTalk.Events;
using Xunit;

namespace TextToTalk.Tests.Events;

public class TextEmitEventTests
{
    [Fact]
    public void IsEquivalent_SupportsSubclasses()
    {
        var e1 = new ChatTextEmitEvent("Somebody", "Something", null, XivChatType.NPCDialogueAnnouncements);
        var e2 = new AddonBattleTalkEmitEvent("Somebody", "Something", null);
        Assert.True(e1.IsEquivalent(e2));
    }

    [Fact]
    public void IsEquivalent_WhenOtherNull_ReturnsFalse()
    {
        var e = new ChatTextEmitEvent("Somebody", "Something", null, XivChatType.Debug);
        Assert.False(e.IsEquivalent(null));
    }
}