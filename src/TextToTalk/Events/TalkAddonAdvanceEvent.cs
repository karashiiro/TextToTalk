namespace TextToTalk.Events;

public class TalkAddonAdvanceEvent : SourcedTextEvent
{
    public TalkAddonAdvanceEvent() : base(TextSource.TalkAddon)
    {
    }
}