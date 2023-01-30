namespace TextToTalk;

public class SharedState
{
    public string LastBattleText { get; set; }
    public string LastQuestText { get; set; }

    public string LastSpeaker { get; set; }

    public bool WSFailedToBindPort { get; set; }

    public nint TalkAddon { get; set; }

    public nint BattleTalkAddon { get; set; }
}