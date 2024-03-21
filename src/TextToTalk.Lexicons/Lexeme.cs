using System.Collections.Generic;

namespace TextToTalk.Lexicons;

public class Lexeme
{
    public required IEnumerable<string> Graphemes { get; init; }

    public required string Phoneme { get; init; }

    public string? Alias { get; init; }
}