using System;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Backends;
using TextToTalk.Middleware;
using TextToTalk.Talk;

namespace TextToTalk.TextProviders;

public class TalkAddonHandler
{
    private record struct TalkAddonState(string? Speaker, string? Text, PollSource PollSource);

    private readonly ClientState clientState;
    private readonly GameGui gui;
    private readonly DataManager data;
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly Condition condition;
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;
    private readonly VoiceBackendManager backendManager;
    private readonly ComponentUpdateState<TalkAddonState> updateState;

    public Action<GameObject?, string?, TextSource> Say { get; set; }

    public TalkAddonHandler(ClientState clientState, GameGui gui, DataManager data, MessageHandlerFilters filters,
        ObjectTable objects, Condition condition, PluginConfiguration config, SharedState sharedState,
        VoiceBackendManager backendManager)
    {
        this.clientState = clientState;
        this.gui = gui;
        this.data = data;
        this.filters = filters;
        this.objects = objects;
        this.condition = condition;
        this.config = config;
        this.sharedState = sharedState;
        this.backendManager = backendManager;
        this.updateState = new ComponentUpdateState<TalkAddonState>();
        this.updateState.OnUpdate += HandleChange;

        Say = (_, _, _) => { };
    }

    public enum PollSource
    {
        None,
        FrameworkUpdate,
        VoiceLinePlayback,
    }

    public void PollAddon(PollSource pollSource)
    {
        var state = GetTalkAddonState(pollSource);
        this.updateState.Mutate(state);
    }

    private void HandleChange(TalkAddonState state)
    {
        var (speaker, text, pollSource) = state;

        // Cancel TTS when the dialogue window is closed, if configured
        if (this.config.CancelSpeechOnTextAdvance)
        {
            this.backendManager.CancelSay(TextSource.TalkAddon);
        }

        // Return early if there's nothing to say
        if (state == default)
        {
            this.filters.SetLastQuestText("");
            return;
        }

        if (this.filters.IsDuplicateQuestText(text)) return;
        this.filters.SetLastQuestText(text);
        DetailedLog.Debug($"AddonTalk: \"{text}\"");

        if (pollSource == PollSource.VoiceLinePlayback && this.config.SkipVoicedQuestText)
        {
            DetailedLog.Debug($"Skipping voice-acted line: {text}");
            return;
        }

        if (this.filters.ShouldProcessSpeaker(speaker))
        {
            var speakerNameToSay = speaker;
            if (this.config.SayPartialName)
            {
                speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, this.config.OnlySayFirstOrLastName);
            }

            text = $"{speakerNameToSay} says {text}";
            this.filters.SetLastSpeaker(speaker);
        }

        var speakerObj = this.objects.FirstOrDefault(gObj => gObj.Name.TextValue == speaker);
        if (!this.filters.ShouldSayFromYou(speaker))
        {
            return;
        }

        // Cancel TTS if it's currently Talk addon text, if configured
        if (this.config.CancelSpeechOnTextAdvance &&
            this.backendManager.GetCurrentlySpokenTextSource() == TextSource.TalkAddon)
        {
            this.backendManager.CancelSay(TextSource.TalkAddon);
        }

        Say(speakerObj, text, TextSource.TalkAddon);
    }

    private unsafe TalkAddonState GetTalkAddonState(PollSource pollSource)
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

        TalkAddonText talkAddonText;
        try
        {
            talkAddonText = TalkUtils.ReadTalkAddon(this.data, talkAddon);
        }
        catch (NullReferenceException)
        {
            // Just swallow the NRE, I have no clue what causes this but it only happens when relogging in rare cases
            return default;
        }

        var text = TalkUtils.NormalizePunctuation(talkAddonText.Text);
        var speaker = TalkUtils.NormalizePunctuation(talkAddonText.Speaker);
        return new TalkAddonState(speaker, text, pollSource);
    }
}