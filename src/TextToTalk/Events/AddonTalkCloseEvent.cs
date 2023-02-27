namespace TextToTalk.Events;

public class AddonTalkCloseEvent : SourcedTextEvent
{
    public AddonTalkCloseEvent() : base(TextSource.AddonTalk)
    {
    }
}