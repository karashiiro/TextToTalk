using TextToTalk.Backends.Websocket;

namespace TextToTalk.Events;

public class NpcDialogueSessionEndedEvent(TextSource source) : NpcDialogueSessionEvent(source)
{
    public override IpcEventType EventType => IpcEventType.NpcDialogueSessionEnded;
}