using System;
using TextToTalk.Backends.Websocket;

namespace TextToTalk.Events;

public abstract class NpcDialogueSessionEvent(TextSource source)
{
    public abstract IpcEventType EventType { get; }

    public Guid SessionId { get; init; }

    public TextSource Source { get; } = source;

    public DialogueEventReason Reason { get; init; }
}