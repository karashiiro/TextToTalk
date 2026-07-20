using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using Moq;
using TextToTalk.Utils;
using Xunit;

namespace TextToTalk.Tests.Utils;

public class SoundSettingsUtilsTests
{
    [Theory]
    [InlineData(100, 100, false, false, 1.0f)]
    [InlineData(50, 100, false, false, 0.5f)]
    [InlineData(100, 50, false, false, 0.5f)]
    [InlineData(50, 50, false, false, 0.25f)]
    [InlineData(0, 100, false, false, 0.0f)]
    [InlineData(100, 0, false, false, 0.0f)]
    [InlineData(100, 100, true, false, 0.0f)]
    [InlineData(100, 100, false, true, 0.0f)]
    [InlineData(100, 100, true, true, 0.0f)]
    [InlineData(0, 0, true, true, 0.0f)]
    public void GetEffectiveVolume_ComputesCorrectValue(
        uint masterVolume, uint voiceVolume, bool masterMuted, bool voiceMuted, float expected)
    {
        var gameConfig = MockGameConfig(masterVolume, voiceVolume, masterMuted, voiceMuted);

        var result = SoundSettingsUtils.GetEffectiveVoiceVolume(gameConfig.Object);

        Assert.Equal(expected, result);
    }

    private static Mock<IGameConfig> MockGameConfig(
        uint masterVolume, uint voiceVolume, bool masterMuted, bool voiceMuted)
    {
        var gameConfig = new Mock<IGameConfig>();

        gameConfig.Setup(gc => gc.TryGet(SystemConfigOption.SoundMaster, out It.Ref<uint>.IsAny))
            .Returns((SystemConfigOption _, out uint val) =>
            {
                val = masterVolume;
                return true;
            });

        gameConfig.Setup(gc => gc.TryGet(SystemConfigOption.SoundVoice, out It.Ref<uint>.IsAny))
            .Returns((SystemConfigOption _, out uint val) =>
            {
                val = voiceVolume;
                return true;
            });

        gameConfig.Setup(gc => gc.TryGet(SystemConfigOption.IsSndMaster, out It.Ref<bool>.IsAny))
            .Returns((SystemConfigOption _, out bool val) =>
            {
                val = masterMuted;
                return true;
            });

        gameConfig.Setup(gc => gc.TryGet(SystemConfigOption.IsSndVoice, out It.Ref<bool>.IsAny))
            .Returns((SystemConfigOption _, out bool val) =>
            {
                val = voiceMuted;
                return true;
            });

        return gameConfig;
    }
}