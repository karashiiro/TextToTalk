namespace TextToTalk.Data.Model;

// TODO: After moving voice presets into LiteDB, create a DbRef from player to voice preset and delete this
public class PlayerVoice
{
    public Guid Id { get; init; }

    public Guid PlayerId { get; init; }

    public int VoicePresetId { get; init; }

    public string? VoiceBackend { get; init; } // Added for composite key
}