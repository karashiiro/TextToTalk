namespace TextToTalk.Events;

public class TalkAddonCloseEvent : SourcedTextEvent
{
    public TalkAddonCloseEvent() : base(TextSource.TalkAddon)
    {
    }
}