using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Talk;

namespace TextToTalk.TextProviders;

public class ChatMessageHandler
{
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;

    public Action<GameObject?, string?, TextSource> Say { get; set; }

    public ChatMessageHandler(MessageHandlerFilters filters, ObjectTable objects, PluginConfiguration config,
        SharedState sharedState)
    {
        this.filters = filters;
        this.objects = objects;
        this.config = config;
        this.sharedState = sharedState;

        Say = (_, _, _) => { };
    }

    public unsafe void ProcessMessage(XivChatType type, uint id, ref SeString? sender, ref SeString message,
        ref bool handled)
    {
        var textValue = message.TextValue;
        if (!this.config.SayPlayerWorldName)
        {
            textValue = TalkUtils.StripWorldFromNames(message);
        }

        textValue = TalkUtils.NormalizePunctuation(textValue);
        if (this.filters.IsDuplicateQuestText(textValue)) return;
        DetailedLog.Debug($"Chat ({type}): \"{textValue}\"");

        if (this.filters.ShouldProcessSpeaker(sender?.TextValue))
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

            var speakerNameToSay = sender!.TextValue;
            if (!this.config.SayPlayerWorldName &&
                sender.Payloads.FirstOrDefault(p => p is PlayerPayload) is PlayerPayload player)
            {
                // Remove world from spoken name
                speakerNameToSay = player.PlayerName;
            }

            if (this.config.SayPartialName)
            {
                speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, this.config.OnlySayFirstOrLastName);
            }

            textValue = $"{speakerNameToSay} says {textValue}";
            this.filters.SetLastSpeaker(sender.TextValue);
        }

        if (IsTextBad(textValue)) return;

        var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();

        var typeAccepted = chatTypes.EnabledChatTypes.Contains((int)type);
        if (!(chatTypes.EnableAllChatTypes || typeAccepted) || this.config.Good.Any() && !IsTextGood(textValue)) return;

        var senderText = sender?.TextValue; // Can't access in lambda
        var speaker = string.IsNullOrEmpty(senderText)
            ? null
            : this.objects.FirstOrDefault(gObj => gObj.Name.TextValue == senderText);
        if (!this.filters.ShouldSayFromYou(speaker?.Name.TextValue)) return;

        Say.Invoke(speaker, textValue, TextSource.Chat);
    }

    private bool IsTextGood(string text)
    {
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