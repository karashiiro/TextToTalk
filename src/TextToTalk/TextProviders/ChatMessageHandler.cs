using System;
using System.Reactive.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using TextToTalk.Events;
using TextToTalk.Middleware;
using TextToTalk.Talk;

namespace TextToTalk.TextProviders;

public class ChatMessageHandler : IDisposable
{
    private record struct ChatMessage(XivChatType Type, SeString Sender, SeString Message);

    private readonly AddonTalkManager addonTalkManager;
    private readonly AddonBattleTalkManager addonBattleTalkManager;
    private readonly MessageHandlerFilters filters;
    private readonly ObjectTable objects;
    private readonly PluginConfiguration config;
    private readonly ChatGui chat;
    private readonly IDisposable subscription;

    public Action<ChatTextEmitEvent> OnTextEmit { get; set; }

    public ChatMessageHandler(AddonTalkManager addonTalkManager, AddonBattleTalkManager addonBattleTalkManager,
        ChatGui chat, MessageHandlerFilters filters, ObjectTable objects, PluginConfiguration config)
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

    private IObservable<ChatMessage> OnChatMessage()
    {
        return Observable.Create((IObserver<ChatMessage> observer) =>
        {
            void HandleMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
            {
                if (!this.config.Enabled) return;
                observer.OnNext(new ChatMessage(type, sender, message));
            }

            this.chat.ChatMessage += HandleMessage;
            return () => { this.chat.ChatMessage -= HandleMessage; };
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

        // Find the game object this speaker is representing
        var speaker = ObjectTableUtils.GetGameObjectByName(this.objects, sender.TextValue);
        if (!this.filters.ShouldSayFromYou(speaker?.Name.TextValue ?? sender.TextValue)) return;

        OnTextEmit.Invoke(new ChatTextEmitEvent(TextSource.Chat, sender, textValue, speaker, type));
    }

    public void Dispose()
    {
        this.subscription.Dispose();
    }
}