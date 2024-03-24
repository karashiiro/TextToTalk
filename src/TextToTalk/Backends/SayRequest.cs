using Dalamud.Game.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends;

public class SayRequest
{
    /// <summary>
    /// The text source.
    /// </summary>
    public required TextSource Source { get; init; }

    /// <summary>
    /// The voice preset to use to execute the request. The expected type varies
    /// depending on the voice backend in use.
    /// </summary>
    public required VoicePreset Voice { get; init; }

    /// <summary>
    /// The speaker's name.
    /// </summary>
    public required string Speaker { get; init; }

    /// <summary>
    /// The speaker's data ID, in the case of NPCs. For most NPCs, their name can
    /// be retrieved from the ENpcResident table, if needed.
    /// </summary>
    public uint? NpcId { get; init; }

    /// <summary>
    /// The spoken text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The chat type, if applicable. Can be from either <see cref="XivChatType"/>
    /// or <see cref="AdditionalChatType"/>.
    /// </summary>
    public XivChatType? ChatType { get; init; }
}