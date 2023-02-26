namespace TextToTalk.Events;

public class SourcedTextEvent : TextEvent
{
    public TextSource Source { get; }

    protected SourcedTextEvent(TextSource source)
    {
        Source = source;
    }
}