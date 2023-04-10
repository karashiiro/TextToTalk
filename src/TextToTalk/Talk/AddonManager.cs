using System;
using System.Reactive.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui;

namespace TextToTalk.Talk;

public abstract class AddonManager : IDisposable
{
    private readonly ClientState clientState;
    private readonly Condition condition;
    private readonly GameGui gui;
    private readonly IDisposable subscription;
    private readonly string name;

    protected nint Address { get; set; }

    protected AddonManager(Framework framework, ClientState clientState, Condition condition, GameGui gui, string name)
    {
        this.clientState = clientState;
        this.condition = condition;
        this.gui = gui;
        this.name = name;

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

    private void UpdateAddonAddress()
    {
        if (!this.clientState.IsLoggedIn || this.condition[ConditionFlag.CreatingCharacter])
        {
            Address = nint.Zero;
            return;
        }

        if (Address == nint.Zero)
        {
            Address = this.gui.GetAddonByName(this.name);
        }
    }

    public void Dispose()
    {
        this.subscription.Dispose();
    }
}