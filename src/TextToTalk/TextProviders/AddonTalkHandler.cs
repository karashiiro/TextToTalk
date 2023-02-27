using System;
using System.Reactive.Linq;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;

namespace TextToTalk.TextProviders;

public class AddonTalkHandler : IDisposable
{
    private record struct AddonTalkState(string? Speaker, string? Text, PollSource PollSource);

    private readonly ClientState clientState;
    private readonly GameGui gui;
    private readonly DataManager data;
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly Condition condition;
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;
    private readonly Framework framework;
    private readonly ComponentUpdateState<AddonTalkState> updateState;
    private readonly IDisposable subscription;

    public Action<TextEmitEvent> OnTextEmit { get; set; }
    public Action<AddonTalkAdvanceEvent> OnAdvance { get; set; }
    public Action<AddonTalkCloseEvent> OnClose { get; set; }

    public AddonTalkHandler(Framework framework, ClientState clientState, GameGui gui, DataManager data,
        MessageHandlerFilters filters, ObjectTable objects, Condition condition, PluginConfiguration config,
        SharedState sharedState)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.gui = gui;
        this.data = data;
        this.filters = filters;
        this.objects = objects;
        this.condition = condition;
        this.config = config;
        this.sharedState = sharedState;
        this.updateState = new ComponentUpdateState<AddonTalkState>();
        this.updateState.OnUpdate += HandleChange;
        this.subscription = HandleFrameworkUpdate();

        OnTextEmit = _ => { };
        OnAdvance = _ => { };
        OnClose = _ => { };
    }

    public enum PollSource
    {
        None,
        FrameworkUpdate,
        VoiceLinePlayback,
    }

    private IObservable<PollSource> OnFrameworkUpdate()
    {
        return Observable.Create((IObserver<PollSource> observer) =>
        {
            void Handle(Framework _)
            {
                if (!this.config.Enabled) return;
                if (!this.config.ReadFromQuestTalkAddon) return;
                observer.OnNext(PollSource.FrameworkUpdate);
            }

            this.framework.Update += Handle;
            return () => { this.framework.Update -= Handle; };
        });
    }

    private IDisposable HandleFrameworkUpdate()
    {
        return OnFrameworkUpdate()
            .Subscribe(PollAddon);
    }

    public void PollAddon(PollSource pollSource)
    {
        var state = GetTalkAddonState(pollSource);
        this.updateState.Mutate(state);
    }

    private void HandleChange(AddonTalkState state)
    {
        var (speaker, text, pollSource) = state;

        if (state == default)
        {
            // The addon was closed
            this.filters.SetLastQuestText("");
            OnClose.Invoke(new AddonTalkCloseEvent());
            return;
        }

        // Notify observers that the addon state was advanced
        OnAdvance.Invoke(new AddonTalkAdvanceEvent());

        text = TalkUtils.NormalizePunctuation(text);

        // Check the quest text (NPCDialogue) state to see if this has already been handled
        if (this.filters.IsDuplicateQuestText(text)) return;
        this.filters.SetLastQuestText(text);

        DetailedLog.Debug($"AddonTalk: \"{text}\"");

        if (pollSource == PollSource.VoiceLinePlayback && this.config.SkipVoicedQuestText)
        {
            DetailedLog.Debug($"Skipping voice-acted line: {text}");
            return;
        }

        // Do postprocessing on the speaker name
        if (this.filters.ShouldProcessSpeaker(speaker))
        {
            this.filters.SetLastSpeaker(speaker);

            var speakerNameToSay = speaker;
            if (this.config.SayPartialName)
            {
                speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, this.config.OnlySayFirstOrLastName);
            }

            text = $"{speakerNameToSay} says {text}";
        }

        // Find the game object this speaker is representing
        var speakerObj = ObjectTableUtils.GetGameObjectByName(this.objects, speaker);
        if (!this.filters.ShouldSayFromYou(speaker)) return;

        OnTextEmit.Invoke(new TextEmitEvent(TextSource.AddonTalk, state.Speaker ?? "", text, speakerObj));
    }

    private unsafe AddonTalkState GetTalkAddonState(PollSource pollSource)
    {
        if (!this.clientState.IsLoggedIn || this.condition[ConditionFlag.CreatingCharacter])
        {
            this.sharedState.TalkAddon = nint.Zero;
            return default;
        }

        if (this.sharedState.TalkAddon == nint.Zero)
        {
            this.sharedState.TalkAddon = this.gui.GetAddonByName("Talk");
            if (this.sharedState.TalkAddon == nint.Zero) return default;
        }

        var talkAddon = (AddonTalk*)this.sharedState.TalkAddon.ToPointer();
        if (talkAddon == null) return default;

        if (!TalkUtils.IsVisible(talkAddon))
        {
            return default;
        }

        AddonTalkText addonTalkText;
        try
        {
            addonTalkText = TalkUtils.ReadTalkAddon(this.data, talkAddon);
        }
        catch (NullReferenceException)
        {
            // Just swallow the NRE, I have no clue what causes this but it only happens when relogging in rare cases
            return default;
        }

        return new AddonTalkState(addonTalkText.Speaker, addonTalkText.Text, pollSource);
    }

    public void Dispose()
    {
        subscription.Dispose();
    }
}