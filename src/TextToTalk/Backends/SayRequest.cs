namespace TextToTalk.Backends;

public class SayRequest
{
    public required TextSource Source { get; init; }

    public required VoicePreset Voice { get; init; }

    public required string Speaker { get; init; }

    public required string Text { get; init; }
}