using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using R3;

namespace TextToTalk.Talk;

public abstract class AddonManager : IDisposable
{
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IGameGui gui;
    private readonly IDisposable subscription;
    private readonly IFramework framework;
    private readonly string name;

    protected nint Address { get; set; }

    protected AddonManager(IFramework framework, IClientState clientState, ICondition condition, IGameGui gui,
        string name)
    {
        this.clientState = clientState;
        this.condition = condition;
        this.gui = gui;
        this.name = name;
        this.framework = framework;

        var onUpdate = Observable.Create(this, static (Observer<IFramework> observer, AddonManager am) =>
        {
            var handler = new IFramework.OnUpdateDelegate(Handle);
            am.framework.Update += handler;
            return new DisposeHandler(() => { am.framework.Update -= handler; });

            void Handle(IFramework f)
            {
                observer.OnNext(f);
            }
        });

        this.subscription = onUpdate.Subscribe(this, static (_, am) => am.UpdateAddonAddress());
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