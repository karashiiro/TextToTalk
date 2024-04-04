using Dalamud;
using Dalamud.Game.Text;
using TextToTalk.Backends.Websocket;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends;

public record SayRequest
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
    /// The speaker's race in the Race table (e.g. "Hyur", "Elezen", etc.).
    /// </summary>
    public required string Race { get; init; }

    /// <summary>
    /// The speaker's age, which can be one of the following:
    /// "Unknown", "Youth", "Adult", "Elder".
    /// </summary>
    public required string Age { get; init; }

    /// <summary>
    /// The spoken text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The message, with the player name replaced with a token.
    ///
    /// Full names are replaced with "{{FULL_NAME}}", first names are replaced with "{{FIRST_NAME}}", and last names
    /// are replaced with "{{LAST_NAME}}".
    /// </summary>
    public required string TextTemplate { get; init; }

    /// <summary>
    /// If stutters were removed from the payload or not.
    /// </summary>
    public bool StuttersRemoved { get; init; }

    /// <summary>
    /// The chat type, if applicable. Can be from either <see cref="XivChatType"/>
    /// or <see cref="AdditionalChatType"/>.
    /// </summary>
    public XivChatType? ChatType { get; init; }

    /// <summary>
    /// The current <see cref="ClientLanguage"/>, to be used for quest dialogues etc.
    /// This does not have any bearing on player chat message languages.
    /// </summary>
    public required ClientLanguage Language { get; init; }

    /// <summary>
    /// The intended IPC message type for this object, only to be used for object mapping.
    /// </summary>
    public IpcMessageType MessageType => IpcMessageType.Say;
}