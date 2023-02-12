using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using TextToTalk.GameEnums;
using TextToTalk.Talk;

namespace TextToTalk.Middleware;

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

    private static string StripWorldFromNames(SeString message)
    {
        // Remove world from all names in message body
        var world = "";
        var cleanString = new SeStringBuilder();
        foreach (var p in message.Payloads)
        {
            switch (p)
            {
                case PlayerPayload pp:
                    world = pp.World.Name;
                    break;
                case TextPayload tp when world != "" && tp.Text != null && tp.Text.Contains(world):
                    cleanString.AddText(tp.Text.Replace(world, ""));
                    break;
                default:
                    cleanString.Add(p);
                    break;
            }
        }

        return cleanString.Build().TextValue;
    }

    public unsafe void ProcessMessage(XivChatType type, uint id, ref SeString? sender, ref SeString message,
        ref bool handled)
    {
        var textValue = message.TextValue;
        if (!config.SayPlayerWorldName)
        {
            textValue = StripWorldFromNames(message);
        }

        textValue = TalkUtils.NormalizePunctuation(textValue);
        if (this.filters.IsDuplicateQuestText(textValue)) return;
        DetailedLog.Debug($"Chat ({type}): \"{textValue}\"");

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

                    var speakerNameToSay = sender.TextValue;

                    if (!config.SayPlayerWorldName &&
                        sender.Payloads.FirstOrDefault(p => p is PlayerPayload) is PlayerPayload player)
                    {
                        // Remove world from spoken name
                        speakerNameToSay = player.PlayerName;
                    }

                    if (this.config.SayPartialName)
                    {
                        speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, config.OnlySayFirstOrLastName);
                    }

                    textValue = $"{speakerNameToSay} says {textValue}";
                    this.filters.SetLastSpeaker(sender.TextValue);
                }
            }
        }

        if (IsTextBad(textValue)) return;

        var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();

        var typeAccepted = chatTypes.EnabledChatTypes.Contains((int)type);
        if (!(chatTypes.EnableAllChatTypes || typeAccepted) || this.config.Good.Count > 0 && !IsTextGood(textValue)) return;

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