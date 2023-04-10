using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui;

namespace TextToTalk.Talk;

public class AddonBattleTalkManager : AddonManager
{
    public AddonBattleTalkManager(Framework framework, ClientState clientState, Condition condition, GameGui gui) :
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