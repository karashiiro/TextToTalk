using Xunit;

namespace TextToTalk.UngenderedOverrides.Tests
{
    public class UngenderedOverrideManagerTests
    {
        [Fact]
        public void UngenderedOverrideManager_Works()
        {
            _ = new UngenderedOverrideManager();
        }

        [Fact]
        public void UngenderedOverrideManager_IsUngendered_ReturnsTrue_When_InOverrideData()
        {
            var manager = new UngenderedOverrideManager("0");
            Assert.True(manager.IsUngendered(0));
        }

        [Fact]
        public void UngenderedOverrideManager_IsUngendered_ReturnsFalse_WhenNot_InOverrideData()
        {
            var manager = new UngenderedOverrideManager("0\n1\n2");
            Assert.False(manager.IsUngendered(5));
        }
    }
}
