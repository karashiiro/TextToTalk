using System;
using System.Reactive.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;

namespace TextToTalk.TextProviders;

public class AddonTalkHandler : IDisposable
{
    private record struct AddonTalkState(string? Speaker, string? Text, PollSource PollSource);

    private readonly AddonTalkManager addonTalkManager;
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly Framework framework;
    private readonly ComponentUpdateState<AddonTalkState> updateState;
    private readonly IDisposable subscription;

    public Action<TextEmitEvent> OnTextEmit { get; set; }
    public Action<AddonTalkAdvanceEvent> OnAdvance { get; set; }
    public Action<AddonTalkCloseEvent> OnClose { get; set; }

    public AddonTalkHandler(AddonTalkManager addonTalkManager, Framework framework,
        MessageHandlerFilters filters, ObjectTable objects, PluginConfiguration config)
    {
        this.addonTalkManager = addonTalkManager;
        this.framework = framework;
        this.filters = filters;
        this.objects = objects;
        this.config = config;
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
            OnClose.Invoke(new AddonTalkCloseEvent());
            return;
        }

        // Notify observers that the addon state was advanced
        OnAdvance.Invoke(new AddonTalkAdvanceEvent());

        text = TalkUtils.NormalizePunctuation(text);

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

    private AddonTalkState GetTalkAddonState(PollSource pollSource)
    {
        if (!this.addonTalkManager.IsVisible())
        {
            return default;
        }

        var addonTalkText = this.addonTalkManager.ReadText();
        return addonTalkText != null
            ? new AddonTalkState(addonTalkText.Speaker, addonTalkText.Text, pollSource)
            : default;
    }

    public void Dispose()
    {
        subscription.Dispose();
    }
}