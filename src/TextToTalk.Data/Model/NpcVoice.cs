namespace TextToTalk.Data.Model;

// TODO: After moving voice presets into LiteDB, create a DbRef from NPC to voice preset and delete this
public class NpcVoice
{
    public Guid Id { get; init; }

    public Guid NpcId { get; init; }

    public int VoicePresetId { get; init; }
}