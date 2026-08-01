using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Utils;

namespace TextToTalk.Talk;

public class AddonSelectIconStringManager : AddonManager, IAddonSelectIconStringManager
{
    public AddonSelectIconStringManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) : base(
        framework, clientState, condition, gui, "SelectIconString")
    {
    }

    public unsafe bool IsVisible()
    {
        var addon = GetAddonSelectIconString();
        return addon != null && addon->AtkUnitBase.IsVisible;
    }

    private unsafe AddonSelectIconString* GetAddonSelectIconString()
    {
        return (AddonSelectIconString*)Address.ToPointer();
    }
}