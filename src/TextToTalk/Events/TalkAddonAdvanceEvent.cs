namespace TextToTalk.Events;

public class TalkAddonAdvanceEvent : TextEvent
{
    public TextSource Source { get; }

    public TalkAddonAdvanceEvent()
    {
        // This will only be emitted by the TalkAddon handler
        Source = TextSource.TalkAddon;
    }
}