namespace TextToTalk.Data.Model;

public class Player
{
    /// <summary>
    /// The local ID of the player (install-specific, not the CID).
    /// </summary>
    public Guid Id { get; init; }

    public string? Name { get; set; }

    public uint WorldId { get; set; }

    public PlayerGender Gender { get; set; }
}