using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace TextToTalk.Utils;

public static class SoundSettingsUtils
{
    public static float GetEffectiveVoiceVolume(IGameConfig gameConfig)
    {
        gameConfig.TryGet(SystemConfigOption.SoundMaster, out uint masterVolume);
        gameConfig.TryGet(SystemConfigOption.SoundVoice, out uint voiceVolume);
        gameConfig.TryGet(SystemConfigOption.IsSndMaster, out bool masterMuted);
        gameConfig.TryGet(SystemConfigOption.IsSndVoice, out bool voiceMuted);

        if (masterMuted || voiceMuted || masterVolume == 0 || voiceVolume == 0)
            return 0f;

        return (masterVolume / 100f) * (voiceVolume / 100f);
    }
}
