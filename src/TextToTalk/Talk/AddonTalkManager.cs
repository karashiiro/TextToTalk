using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Utils;

namespace TextToTalk.Talk;

public class AddonTalkManager : AddonManager
{
    public AddonTalkManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) : base(
        framework, clientState, condition, gui, "Talk")
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

    private unsafe AddonTalk* GetAddonTalk()
    {
        return (AddonTalk*)Address.ToPointer();
    }
}