using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Text;
using System.Reflection.Metadata.Ecma335;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using R3;
using Serilog;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;
using TextToTalk.Utils;

namespace TextToTalk.TextProviders;

public class ChatMessageHandler : IChatMessageHandler
{
    private record struct ChatMessage(XivChatType Type, SeString Sender, SeString Message);

    private readonly AddonTalkManager addonTalkManager;
    private readonly AddonBattleTalkManager addonBattleTalkManager;
    private readonly MessageHandlerFilters filters;
    private readonly IObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly IChatGui chat;
    private readonly IDisposable subscription;
    private readonly IClientState clientState;

    public Action<ChatTextEmitEvent> OnTextEmit { get; set; }

    public ChatMessageHandler(AddonTalkManager addonTalkManager, AddonBattleTalkManager addonBattleTalkManager,
        IChatGui chat, MessageHandlerFilters filters, IObjectTable objects, PluginConfiguration config)
    {
        this.addonTalkManager = addonTalkManager;
        this.addonBattleTalkManager = addonBattleTalkManager;
        this.filters = filters;
        this.objects = objects;
        this.config = config;
        this.chat = chat;


        this.subscription = HandleChatMessage();

        OnTextEmit = _ => { };
    }

    private Observable<ChatMessage> OnChatMessage()
    {
        return Observable.Create(this, static (Observer<ChatMessage> observer, ChatMessageHandler cmh) =>
        {
            var handler = new IChatGui.OnMessageDelegate(HandleMessage);
            cmh.chat.ChatMessage += handler;
            return Disposable.Create(() => { cmh.chat.ChatMessage -= handler; });

            void HandleMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message,
                ref bool handled)
            {
                if (!cmh.config.Enabled) return;
                observer.OnNext(new ChatMessage(type, sender, message));
            }
        });
    }

    private IDisposable HandleChatMessage()
    {
        return OnChatMessage()
            .Subscribe(ProcessChatMessage);
    }

    private void ProcessChatMessage(ChatMessage chatMessage)
    {
        var (type, sender, message) = chatMessage;
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
            if (this.config.ReadFromQuestTalkAddon && this.addonTalkManager.IsVisible())
            {
                return;
            }
        }
        else if (type == XivChatType.NPCDialogueAnnouncements)
        {
            if (this.config.ReadFromBattleTalkAddon && this.addonBattleTalkManager.IsVisible())
            {
                return;
            }
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

        // Find the game object this speaker represents
        var speaker = ObjectTableUtils.GetGameObjectByName(this.objects, sender);

        if (!this.filters.OnlyMessagesFromYou(speaker?.Name.TextValue ?? sender.TextValue)) return;

        if (!this.filters.ShouldSayFromYou(speaker?.Name.TextValue ?? sender.TextValue)) return;
        
        else if (type == XivChatType.TellOutgoing && config.SkipMessagesFromYou == true) return;

            OnTextEmit.Invoke(new ChatTextEmitEvent(
                GetCleanSpeakerName(speaker, sender),
                textValue,
                speaker,
                type));
    }

    private static SeString GetCleanSpeakerName(IGameObject? speaker, SeString sender)
    {
        // Get the speaker name from their entity data, if possible
        if (speaker != null)
        {
            return speaker.Name;
        }

        // Parse the speaker name from chat and hope it's right
        return TalkUtils.TryGetEntityName(sender, out var senderName) ? senderName : sender;
    }

    public void Dispose()
    {
        this.subscription.Dispose();
    }

    Observable<ChatTextEmitEvent> IChatMessageHandler.OnTextEmit()
    {
        return Observable.FromEvent<ChatTextEmitEvent>(
            h => OnTextEmit += h,
            h => OnTextEmit -= h);
    }
}