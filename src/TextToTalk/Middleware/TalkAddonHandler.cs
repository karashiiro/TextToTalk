using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using Dalamud.Logging;
using TextToTalk.Backends;
using TextToTalk.Talk;

namespace TextToTalk.Middleware;

public class TalkAddonHandler
{
    private readonly ClientState clientState;
    private readonly GameGui gui;
    private readonly DataManager data;
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;
    private readonly VoiceBackendManager backendManager;

    public Action<GameObject, string, TextSource> Say { get; set; }

    public TalkAddonHandler(ClientState clientState, GameGui gui, DataManager data, MessageHandlerFilters filters,
        ObjectTable objects, PluginConfiguration config, SharedState sharedState, VoiceBackendManager backendManager)
    {
        this.clientState = clientState;
        this.gui = gui;
        this.data = data;
        this.filters = filters;
        this.objects = objects;
        this.config = config;
        this.sharedState = sharedState;
        this.backendManager = backendManager;
    }

    public enum PollSource
    {
        FrameworkUpdate,
        VoiceLinePlayback,
    }

    public unsafe void PollAddon(PollSource pollSource)
    {
        if (!this.clientState.IsLoggedIn)
        {
            this.sharedState.TalkAddon = nint.Zero;
            return;
        }

        if (this.sharedState.TalkAddon == nint.Zero)
        {
            this.sharedState.TalkAddon = this.gui.GetAddonByName("Talk", 1);
            if (this.sharedState.TalkAddon == nint.Zero) return;
        }

        var talkAddon = (AddonTalk*)this.sharedState.TalkAddon.ToPointer();
        if (talkAddon == null) return;

        // Clear the last text if the window isn't visible.
        if (!TalkUtils.IsVisible(talkAddon))
        {
            // Cancel TTS when the dialogue window is closed, if configured
            if (this.config.CancelSpeechOnTextAdvance)
            {
                this.backendManager.CancelSay(TextSource.TalkAddon);
            }

            this.filters.SetLastQuestText("");
            return;
        }

        TalkAddonText talkAddonText;
        try
        {
            talkAddonText = TalkUtils.ReadTalkAddon(this.data, talkAddon);
        }
        catch (NullReferenceException)
        {
            // Just swallow the NRE, I have no clue what causes this but it only happens when relogging in rare cases
            return;
        }

        var text = TalkUtils.NormalizePunctuation(talkAddonText.Text);
        if (text == "" || this.filters.IsDuplicateQuestText(text)) return;
        this.filters.SetLastQuestText(text);
        PluginLog.LogDebug($"AddonTalk: \"{text}\"");

        if (pollSource == PollSource.VoiceLinePlayback && this.config.SkipVoicedQuestText)
        {
            PluginLog.Log($"Skipping voice-acted line: {text}");
            return;
        }

        if (talkAddonText.Speaker != "" && this.filters.ShouldSaySender())
        {
            if (!this.config.DisallowMultipleSay || !this.filters.IsSameSpeaker(talkAddonText.Speaker))
            {
                var speakerNameToSay = talkAddonText.Speaker;

                if (config.SayPartialName)
                {
                    speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, config.OnlySayFirstOrLastName);
                }

                text = $"{speakerNameToSay} says {text}";
                this.filters.SetLastSpeaker(talkAddonText.Speaker);
            }
        }

        var speaker = this.objects.FirstOrDefault(gObj => gObj.Name.TextValue == talkAddonText.Speaker);
        if (!this.filters.ShouldSayFromYou(speaker?.Name.TextValue))
        {
            return;
        }

        // Cancel TTS if it's currently Talk addon text, if configured
        if (this.config.CancelSpeechOnTextAdvance &&
            this.backendManager.GetCurrentlySpokenTextSource() == TextSource.TalkAddon)
        {
            this.backendManager.CancelSay(TextSource.TalkAddon);
        }

        Say?.Invoke(speaker, text, TextSource.TalkAddon);
    }
}