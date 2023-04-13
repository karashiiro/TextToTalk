namespace TextToTalk.Data.Model;

public class NpcVoice
{
    public Guid Id { get; init; }

    public Guid NpcId { get; init; }

    public int VoicePresetId { get; init; }
}