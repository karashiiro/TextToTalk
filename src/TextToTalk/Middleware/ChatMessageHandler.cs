using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using TextToTalk.GameEnums;
using TextToTalk.Talk;

namespace TextToTalk.Middleware;

public class ChatMessageHandler
{
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;

    public Action<GameObject, string, TextSource> Say { get; set; }

    public ChatMessageHandler(MessageHandlerFilters filters, ObjectTable objects, PluginConfiguration config, SharedState sharedState)
    {
        this.filters = filters;
        this.objects = objects;
        this.config = config;
        this.sharedState = sharedState;
    }

    public unsafe void ProcessMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
    {
        var textValue = message.TextValue;
        if (this.filters.IsDuplicateQuestText(textValue)) return;

#if DEBUG
        PluginLog.Log("Chat message from type {0}: {1}", type, textValue);
#endif

        // This section controls speaker-related functions.
        if (sender != null && sender.TextValue != string.Empty)
        {
            if (this.filters.ShouldSaySender(type))
            {
                // If we allow the speaker's name to be repeated each time the speak,
                // or the speaker has actually changed.
                if (!this.config.DisallowMultipleSay || !this.filters.IsSameSpeaker(sender.TextValue))
                {
                    if ((int)type == (int)AdditionalChatType.NPCDialogue)
                    {
                        // (TextToTalk#40) If we're reading from the Talk addon when NPC dialogue shows up, just return from this.
                        var talkAddon = (AddonTalk*)this.sharedState.TalkAddon.ToPointer();
                        if (this.config.ReadFromQuestTalkAddon && talkAddon != null && TalkUtils.IsVisible(talkAddon))
                        {
                            return;
                        }

                        this.filters.SetLastQuestText(textValue);
                    }

                    textValue = $"{sender.TextValue} says {textValue}";
                    this.filters.SetLastSpeaker(sender.TextValue);
                }
            }
        }

        if (this.config.Bad.Where(t => t.Text != "").Any(t => t.Match(textValue))) return;

        var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();

        var typeAccepted = chatTypes.EnabledChatTypes.Contains((int)type);
        var goodMatch = this.config.Good
            .Where(t => t.Text != "")
            .Any(t => t.Match(textValue));
        if (!(chatTypes.EnableAllChatTypes || typeAccepted) || this.config.Good.Count > 0 && !goodMatch) return;

        var senderText = sender?.TextValue; // Can't access in lambda
        var speaker = this.objects.FirstOrDefault(a => a.Name.TextValue == senderText);

        Say?.Invoke(speaker, textValue, TextSource.Chat);
    }
}