using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Utils;

namespace TextToTalk.Talk;

public class AddonSelectStringManager : AddonManager, IAddonSelectStringManager
{
    public AddonSelectStringManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui) : base(
        framework, clientState, condition, gui, "SelectString")
    {
    }

    public unsafe bool IsVisible()
    {
        var addon = GetAddonSelectString();
        return addon != null && addon->AtkUnitBase.IsVisible;
    }

    private unsafe AddonSelectString* GetAddonSelectString()
    {
        return (AddonSelectString*)Address.ToPointer();
    }
}