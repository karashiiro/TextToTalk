namespace TextToTalk.Events;

public class TextEmitEvent : TextEvent
{
    public TextSource Source { get; }

    public string SpeakerName { get; }

    public string Text { get; }

    protected TextEmitEvent(TextSource source, string speaker, string text)
    {
        Source = source;
        SpeakerName = speaker;
        Text = text;
    }
}