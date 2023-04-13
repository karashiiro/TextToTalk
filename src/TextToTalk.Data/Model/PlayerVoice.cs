namespace TextToTalk.Data.Model;

public class PlayerVoice
{
    public Guid Id { get; init; }

    public Guid PlayerId { get; init; }

    public int VoicePresetId { get; init; }
}