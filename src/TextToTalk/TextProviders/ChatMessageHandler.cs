using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;

namespace TextToTalk.TextProviders;

public class ChatMessageHandler
{
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly PluginConfiguration config;

    public Action<ChatTextEmitEvent> OnTextEmit { get; set; }

    public ChatMessageHandler(MessageHandlerFilters filters, ObjectTable objects, PluginConfiguration config)
    {
        this.filters = filters;
        this.objects = objects;
        this.config = config;

        OnTextEmit = _ => { };
    }

    public void ProcessMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
    {
        var textValue = message.TextValue;

        if (!this.config.SayPlayerWorldName)
        {
            // Strip worlds from any player names
            textValue = TalkUtils.StripWorldFromNames(message);
        }

        textValue = TalkUtils.NormalizePunctuation(textValue);

        DetailedLog.Debug($"Chat ({type}): \"{textValue}\"");

        if (type == XivChatType.NPCDialogue)
        {
            // (TextToTalk#40) If we're reading from the Talk addon when NPC dialogue shows up, just return from this.
            if (this.filters.IsTalkAddonActive())
            {
                return;
            }

            // Check the quest text (NPCDialogue) state to see if this has already been handled
            // TODO: Is this redundant with the above check in place?
            if (this.filters.IsDuplicateQuestText(textValue)) return;
            this.filters.SetLastQuestText(textValue);
        }

        // Do postprocessing on the speaker name
        if (this.filters.ShouldProcessSpeaker(sender.TextValue))
        {
            this.filters.SetLastSpeaker(sender.TextValue);

            var speakerNameToSay = sender.TextValue;
            if (!this.config.SayPlayerWorldName)
            {
                speakerNameToSay = TalkUtils.GetPlayerNameWithoutWorld(sender);
            }

            if (this.config.SayPartialName)
            {
                speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, this.config.OnlySayFirstOrLastName);
            }

            textValue = $"{speakerNameToSay} says {textValue}";
        }

        // Check all of the other filters to see if this should be dropped
        var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();
        var typeAccepted = chatTypes.EnableAllChatTypes || chatTypes.EnabledChatTypes.Contains((int)type);
        if (!typeAccepted || IsTextBad(textValue) || !IsTextGood(textValue)) return;

        // Find the game object this speaker is representing
        var speaker = ObjectTableUtils.GetGameObjectByName(this.objects, sender.TextValue);
        if (!this.filters.ShouldSayFromYou(speaker?.Name.TextValue ?? sender.TextValue)) return;

        OnTextEmit.Invoke(new ChatTextEmitEvent(TextSource.Chat, sender, textValue, speaker, type));
    }

    private bool IsTextGood(string text)
    {
        if (!this.config.Good.Any())
        {
            return true;
        }

        return this.config.Good
            .Where(t => t.Text != "")
            .Any(t => t.Match(text));
    }

    private bool IsTextBad(string text)
    {
        return this.config.Bad
            .Where(t => t.Text != "")
            .Any(t => t.Match(text));
    }
}