using System;
using System.Reactive.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace TextToTalk.Talk;

public abstract class AddonManager : IDisposable
{
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IGameGui gui;
    private readonly IDisposable subscription;
    private readonly string name;

    protected nint Address { get; set; }

    protected AddonManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui, string name)
    {
        this.clientState = clientState;
        this.condition = condition;
        this.gui = gui;
        this.name = name;

        var onUpdate = Observable.Create((IObserver<IFramework> observer) =>
        {
            void Handle(IFramework f)
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