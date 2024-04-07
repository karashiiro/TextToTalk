using System;
using Dalamud.Plugin.Services;
using R3;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;
using TextToTalk.Utils;

namespace TextToTalk.TextProviders;

public class AddonTalkHandler : IAddonTalkHandler
{
    private record struct AddonTalkState(string? Speaker, string? Text, AddonPollSource PollSource, string? VoiceFile);

    private readonly AddonTalkManager addonTalkManager;
    private readonly MessageHandlerFilters filters;
    private readonly IObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly IFramework framework;
    private readonly ComponentUpdateState<AddonTalkState> updateState;
    private readonly IDisposable subscription;

    // Most recent speaker/text specific to this addon
    private string? lastAddonSpeaker;
    private string? lastAddonText;

    public Action<TextEmitEvent> OnTextEmit { get; set; }
    public Action<AddonTalkAdvanceEvent> OnAdvance { get; set; }
    public Action<AddonTalkCloseEvent> OnClose { get; set; }

    public AddonTalkHandler(AddonTalkManager addonTalkManager, IFramework framework,
        MessageHandlerFilters filters, IObjectTable objects, PluginConfiguration config)
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

    private Observable<AddonPollSource> OnFrameworkUpdate()
    {
        return Observable.Create(this, static (Observer<AddonPollSource> observer, AddonTalkHandler ath) =>
        {
            var handler = new IFramework.OnUpdateDelegate(Handle);
            ath.framework.Update += handler;
            return Disposable.Create(() => { ath.framework.Update -= handler; });

            void Handle(IFramework _)
            {
                if (!ath.config.Enabled) return;
                if (!ath.config.ReadFromQuestTalkAddon) return;
                observer.OnNext(AddonPollSource.FrameworkUpdate);
            }
        });
    }

    private IDisposable HandleFrameworkUpdate()
    {
        return OnFrameworkUpdate().Subscribe(this, static (s, h) => h.PollAddon(s, null));
    }

    public void PollAddon(AddonPollSource pollSource, string? voiceFile)
    {
        var state = GetTalkAddonState(pollSource, voiceFile);
        this.updateState.Mutate(state);
    }

    private void HandleChange(AddonTalkState state)
    {
        var (speaker, text, pollSource, voiceFile) = state;

        if (state == default)
        {
            // The addon was closed
            OnClose.Invoke(new AddonTalkCloseEvent());
            return;
        }

        // Notify observers that the addon state was advanced
        OnAdvance.Invoke(new AddonTalkAdvanceEvent());

        text = TalkUtils.NormalizePunctuation(text);

        DetailedLog.Debug($"AddonTalk ({pollSource}): \"{text}\"");

        {
            // This entire callback executes twice in a row - once for the voice line, and then again immediately
            // afterwards for the framework update itself. This prevents the second invocation from being spoken.
            if (this.lastAddonSpeaker == speaker && this.lastAddonText == text)
            {
                DetailedLog.Debug($"Skipping duplicate line: {text}");
                return;
            }

            this.lastAddonSpeaker = speaker;
            this.lastAddonText = text;
        }

        if (pollSource == AddonPollSource.VoiceLinePlayback && this.config.SkipVoicedQuestText)
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
        var speakerObj = speaker != null ? ObjectTableUtils.GetGameObjectByName(this.objects, speaker) : null;
        if (!this.filters.ShouldSayFromYou(speaker)) return;

        OnTextEmit.Invoke(speakerObj != null
            ? new AddonTalkEmitEvent(speakerObj.Name, text, speakerObj, voiceFile)
            : new AddonTalkEmitEvent(state.Speaker ?? "", text, null, voiceFile));
    }

    private AddonTalkState GetTalkAddonState(AddonPollSource pollSource, string? voiceFile)
    {
        if (!this.addonTalkManager.IsVisible())
        {
            return default;
        }

        var addonTalkText = this.addonTalkManager.ReadText();
        return addonTalkText != null
            ? new AddonTalkState(addonTalkText.Speaker, addonTalkText.Text, pollSource, voiceFile)
            : default;
    }

    public void Dispose()
    {
        subscription.Dispose();
    }

    Observable<TextEmitEvent> IAddonTalkHandler.OnTextEmit()
    {
        return Observable.FromEvent<TextEmitEvent>(
            h => OnTextEmit += h,
            h => OnTextEmit -= h);
    }

    Observable<AddonTalkAdvanceEvent> IAddonTalkHandler.OnAdvance()
    {
        return Observable.FromEvent<AddonTalkAdvanceEvent>(
            h => OnAdvance += h,
            h => OnAdvance -= h);
    }

    Observable<AddonTalkCloseEvent> IAddonTalkHandler.OnClose()
    {
        return Observable.FromEvent<AddonTalkCloseEvent>(
            h => OnClose += h,
            h => OnClose -= h);
    }
}