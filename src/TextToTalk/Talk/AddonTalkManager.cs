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

    public unsafe void Advance()
    {
        var addonTalk = GetAddonTalk();
        if (addonTalk == null || !addonTalk->AtkUnitBase.IsVisible) return;

        var stage = FFXIVClientStructs.FFXIV.Component.GUI.AtkStage.Instance();
        if (stage == null) return;

        var target = &stage->AtkEventTarget;

        var evt = new FFXIVClientStructs.FFXIV.Component.GUI.AtkEvent
        {
            Target = target,
            Listener = (FFXIVClientStructs.FFXIV.Component.GUI.AtkEventListener*)addonTalk,
        };
        evt.State.StateFlags = (FFXIVClientStructs.FFXIV.Component.GUI.AtkEventStateFlags)132;

        var data = new FFXIVClientStructs.FFXIV.Component.GUI.AtkEventData();

        var listener = (FFXIVClientStructs.FFXIV.Component.GUI.AtkEventListener*)addonTalk;
        listener->ReceiveEvent(FFXIVClientStructs.FFXIV.Component.GUI.AtkEventType.MouseDown, 0, &evt, &data);
        listener->ReceiveEvent(FFXIVClientStructs.FFXIV.Component.GUI.AtkEventType.MouseClick, 0, &evt, &data);
        listener->ReceiveEvent(FFXIVClientStructs.FFXIV.Component.GUI.AtkEventType.MouseUp, 0, &evt, &data);
    }

    private unsafe AddonTalk* GetAddonTalk()
    {
        return (AddonTalk*)Address.ToPointer();
    }
}