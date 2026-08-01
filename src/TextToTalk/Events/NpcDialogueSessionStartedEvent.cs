using TextToTalk.Backends.Websocket;

namespace TextToTalk.Events;

public class NpcDialogueSessionStartedEvent(TextSource source) : NpcDialogueSessionEvent(source)
{
    public override IpcEventType EventType => IpcEventType.NpcDialogueSessionStarted;
}