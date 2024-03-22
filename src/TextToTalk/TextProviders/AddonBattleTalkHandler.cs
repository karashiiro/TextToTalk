using System;
using Dalamud.Plugin.Services;
using R3;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;
using TextToTalk.Utils;

namespace TextToTalk.TextProviders;

// This might be almost exactly the same as AddonTalkHandler, but it's too early to pull out a common base class.
public class AddonBattleTalkHandler : IDisposable
{
    private record struct AddonBattleTalkState(string? Speaker, string? Text, AddonPollSource PollSource);

    private readonly AddonBattleTalkManager addonTalkManager;
    private readonly MessageHandlerFilters filters;
    private readonly IObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly IFramework framework;
    private readonly ComponentUpdateState<AddonBattleTalkState> updateState;
    private readonly IDisposable subscription;

    // Most recent speaker/text specific to this addon
    private string? lastAddonSpeaker;
    private string? lastAddonText;

    public Action<TextEmitEvent> OnTextEmit { get; set; }

    public AddonBattleTalkHandler(AddonBattleTalkManager addonTalkManager, IFramework framework,
        MessageHandlerFilters filters, IObjectTable objects, PluginConfiguration config)
    {
        this.addonTalkManager = addonTalkManager;
        this.framework = framework;
        this.filters = filters;
        this.objects = objects;
        this.config = config;
        this.updateState = new ComponentUpdateState<AddonBattleTalkState>();
        this.updateState.OnUpdate += HandleChange;
        this.subscription = HandleFrameworkUpdate();

        OnTextEmit = _ => { };
    }

    private Observable<AddonPollSource> OnFrameworkUpdate()
    {
        return Observable.Create(this, static (Observer<AddonPollSource> observer, AddonBattleTalkHandler abth) =>
        {
            var handler = new IFramework.OnUpdateDelegate(Handle);
            abth.framework.Update += handler;
            return Disposable.Create(() => abth.framework.Update -= handler);

            void Handle(IFramework f)
            {
                if (!abth.config.Enabled) return;
                if (!abth.config.ReadFromBattleTalkAddon) return;
                observer.OnNext(AddonPollSource.FrameworkUpdate);
            }
        });
    }

    private IDisposable HandleFrameworkUpdate()
    {
        return OnFrameworkUpdate().Subscribe(this, static (s, h) => h.PollAddon(s));
    }

    public void PollAddon(AddonPollSource pollSource)
    {
        var state = GetTalkAddonState(pollSource);
        this.updateState.Mutate(state);
    }

    private void HandleChange(AddonBattleTalkState state)
    {
        var (speaker, text, pollSource) = state;

        if (state == default)
        {
            // The addon was closed
            return;
        }

        text = TalkUtils.NormalizePunctuation(text);

        DetailedLog.Debug($"AddonBattleTalk ({pollSource}): \"{text}\"");

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

        if (pollSource == AddonPollSource.VoiceLinePlayback && this.config.SkipVoicedBattleText)
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
            ? new TextEmitEvent(TextSource.AddonTalk, speakerObj.Name, text, speakerObj)
            : new TextEmitEvent(TextSource.AddonTalk, state.Speaker ?? "", text, null));
    }

    private AddonBattleTalkState GetTalkAddonState(AddonPollSource pollSource)
    {
        if (!this.addonTalkManager.IsVisible())
        {
            return default;
        }

        var addonTalkText = this.addonTalkManager.ReadText();
        return addonTalkText != null
            ? new AddonBattleTalkState(addonTalkText.Speaker, addonTalkText.Text, pollSource)
            : default;
    }

    public void Dispose()
    {
        subscription.Dispose();
    }
}