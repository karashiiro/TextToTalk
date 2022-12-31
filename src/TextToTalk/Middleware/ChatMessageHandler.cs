using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
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

    public Action<GameObject, string, TextSource> Say { get; set; }

    public ChatMessageHandler(MessageHandlerFilters filters, ObjectTable objects, PluginConfiguration config,
        SharedState sharedState)
    {
        this.filters = filters;
        this.objects = objects;
        this.config = config;
        this.sharedState = sharedState;
    }

    public unsafe void ProcessMessage(XivChatType type, uint id, ref SeString sender, ref SeString message,
        ref bool handled)
    {
        var textValue = message.TextValue;
        if (!config.SayPlayerWorldName)
        {
            // Remove world from all names in message body
            var world = "";
            var cleanString = new SeStringBuilder();
            foreach (var p in message.Payloads)
            {
                if (p is PlayerPayload pp)
                {
                    world = pp.World.Name;
                }
                else if (p is TextPayload tp)
                {
                    if (world != "" && tp.Text?.Contains(world) == true)
                    {
                        cleanString.AddText(tp.Text.Replace(world, ""));
                    }
                    else
                    {
                        cleanString.Add(p);
                    }
                }
                else
                {
                    cleanString.Add(p);
                }
            }

            textValue = cleanString.Build().TextValue;
        }

        textValue = TalkUtils.NormalizePunctuation(textValue);
        if (this.filters.IsDuplicateQuestText(textValue)) return;
        PluginLog.LogDebug($"Chat ({type}): \"{textValue}\"");

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

                    if (config.SayPartialName)
                    {
                        speakerNameToSay = TalkUtils.GetPartialName(speakerNameToSay, config.OnlySayFirstOrLastName);
                    }

                    textValue = $"{speakerNameToSay} says {textValue}";
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
        var speaker = string.IsNullOrEmpty(senderText)
            ? null
            : this.objects.FirstOrDefault(gObj => gObj.Name.TextValue == senderText);
        if (!this.filters.ShouldSayFromYou(speaker?.Name.TextValue))
        {
            return;
        }

        Say?.Invoke(speaker, textValue, TextSource.Chat);
    }
}