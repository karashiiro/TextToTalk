using Dalamud.Plugin.Services;
using TextToTalk.Utils;

namespace TextToTalk.Talk;

public class AddonBattleTalkManager : AddonManager
{
    public AddonBattleTalkManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) :
        base(framework, clientState, condition, gui, "_BattleTalk")
    {
    }

    public unsafe AddonTalkText? ReadText()
    {
        var addonTalk = GetAddonTalk();
        return addonTalk == null ? null : TalkUtils.ReadTalkAddon(addonTalk);
    }

    public unsafe bool IsVisible()
    {
        var addonTalk = GetAddonTalk();
        return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
    }

    private unsafe AddonBattleTalk* GetAddonTalk()
    {
        return (AddonBattleTalk*)Address.ToPointer();
    }
}