namespace TextToTalk;

public class SharedState
{
    public string LastQuestText { get; set; }

    public string LastSpeaker { get; set; }

    public bool WSFailedToBindPort { get; set; }

    public nint TalkAddon { get; set; }
}