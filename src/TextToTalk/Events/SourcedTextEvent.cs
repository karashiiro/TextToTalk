namespace TextToTalk.Events;

public class SourcedTextEvent : TextEvent
{
    /// <summary>
    /// The source of the event.
    /// </summary>
    public TextSource Source { get; }

    protected SourcedTextEvent(TextSource source)
    {
        Source = source;
    }
}