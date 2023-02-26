using System;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Events;
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
    private readonly ComponentUpdateState<TalkAddonState> updateState;

    public Action<TextEmitEvent> OnTextEmit { get; set; }
    public Action<TalkAddonAdvanceEvent> OnAdvance { get; set; }
    public Action<TalkAddonCloseEvent> OnClose { get; set; }

    public TalkAddonHandler(ClientState clientState, GameGui gui, DataManager data, MessageHandlerFilters filters,
        ObjectTable objects, Condition condition, PluginConfiguration config, SharedState sharedState)
    {
        this.clientState = clientState;
        this.gui = gui;
        this.data = data;
        this.filters = filters;
        this.objects = objects;
        this.condition = condition;
        this.config = config;
        this.sharedState = sharedState;
        this.updateState = new ComponentUpdateState<TalkAddonState>();
        this.updateState.OnUpdate += HandleChange;

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

    public void PollAddon(PollSource pollSource)
    {
        var state = GetTalkAddonState(pollSource);
        this.updateState.Mutate(state);
    }

    private void HandleChange(TalkAddonState state)
    {
        var (speaker, text, pollSource) = state;

        if (state == default)
        {
            // The addon was closed
            this.filters.SetLastQuestText("");
            OnClose.Invoke(new TalkAddonCloseEvent());
            return;
        }

        // Notify observers that the addon state was advanced
        OnAdvance.Invoke(new TalkAddonAdvanceEvent());

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

        OnTextEmit.Invoke(new TextEmitEvent(TextSource.TalkAddon, state.Speaker ?? "", text, speakerObj));
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

        return new TalkAddonState(talkAddonText.Speaker, talkAddonText.Text, pollSource);
    }
}