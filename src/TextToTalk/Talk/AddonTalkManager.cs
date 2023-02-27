using System;
using System.Reactive.Linq;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace TextToTalk.Talk;

public class AddonTalkManager : IDisposable
{
    private readonly ClientState clientState;
    private readonly Condition condition;
    private readonly DataManager data;
    private readonly GameGui gui;
    private readonly IDisposable subscription;

    private nint address;

    public AddonTalkManager(Framework framework, ClientState clientState, Condition condition, DataManager data,
        GameGui gui)
    {
        this.clientState = clientState;
        this.condition = condition;
        this.data = data;
        this.gui = gui;

        var onUpdate = Observable.Create((IObserver<Framework> observer) =>
        {
            void Handle(Framework f)
            {
                observer.OnNext(f);
            }

            framework.Update += Handle;
            return () => { framework.Update -= Handle; };
        });

        this.subscription = onUpdate
            .Subscribe(_ => UpdateAddonAddress());
    }

    public unsafe AddonTalkText? ReadText()
    {
        var addonTalk = GetAddonTalk();
        return addonTalk == null ? null : TalkUtils.ReadTalkAddon(this.data, addonTalk);
    }

    public unsafe bool IsVisible()
    {
        var addonTalk = GetAddonTalk();
        return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
    }

    private void UpdateAddonAddress()
    {
        if (!this.clientState.IsLoggedIn || this.condition[ConditionFlag.CreatingCharacter])
        {
            this.address = nint.Zero;
            return;
        }

        if (this.address == nint.Zero)
        {
            this.address = this.gui.GetAddonByName("Talk");
        }
    }

    private unsafe AddonTalk* GetAddonTalk()
    {
        return (AddonTalk*)this.address.ToPointer();
    }

    public void Dispose()
    {
        this.subscription.Dispose();
    }
}