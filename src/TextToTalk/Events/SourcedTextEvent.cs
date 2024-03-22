namespace TextToTalk.Events;

public abstract class SourcedTextEvent(TextSource source) : TextEvent
{
    /// <summary>
    /// The source of the event.
    /// </summary>
    public TextSource Source { get; } = source;
}