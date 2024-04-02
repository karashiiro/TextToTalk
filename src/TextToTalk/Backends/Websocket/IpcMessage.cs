using System;
using Dalamud;
using Dalamud.Game.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends.Websocket;

[Serializable]
public class IpcMessage(
    IpcMessageType type,
    string speaker,
    string payload,
    string payloadTemplate,
    VoicePreset? preset,
    TextSource source,
    ClientLanguage? clientLanguage,
    bool stuttersRemoved,
    long? npcId,
    XivChatType? chatType) : IEquatable<IpcMessage>
{
    /// <summary>
    /// The speaker name.
    /// </summary>
    public string Speaker { get; init; } = speaker;

    /// <summary>
    /// The speaker's data ID, in the case of NPCs. For most NPCs, their name can
    /// be retrieved from the ENpcResident table, if needed.
    /// </summary>
    public long? NpcId { get; init; } = npcId;

    /// <summary>
    /// The message type; refer tp <see cref="IpcMessageType"/> for options.
    /// </summary>
    public string Type { get; init; } = type.ToString();

    /// <summary>
    /// The message parameter - the spoken text for speech requests, and an empty string for cancellations.
    /// </summary>
    public string Payload { get; init; } = payload;

    /// <summary>
    /// The message, with the player name replaced with a token.
    ///
    /// Full names are replaced with "{{FULL_NAME}}", first names are replaced with "{{FIRST_NAME}}", and last names
    /// are replaced with "{{LAST_NAME}}".
    /// </summary>
    public string PayloadTemplate { get; init; } = payloadTemplate;

    /// <summary>
    /// Speaker voice ID.
    /// </summary>
    public VoicePreset? Voice { get; init; } = preset;

    /// <summary>
    /// If stutters were removed from the payload or not.
    /// </summary>
    public bool StuttersRemoved { get; init; } = stuttersRemoved;

    /// <summary>
    /// Text source; refer to <see cref="TextSource"/> for options.
    /// </summary>
    public string Source { get; init; } = source.ToString();

    /// <summary>
    /// The chat type, if applicable. Can be from either <see cref="XivChatType"/>
    /// or <see cref="AdditionalChatType"/>.
    /// </summary>
    public int? ChatType { get; init; } = (int?)chatType;

    /// <summary>
    /// The client's current language, from <see cref="ClientLanguage"/>. This will
    /// be null for cancel messages.
    /// </summary>
    public string? Language { get; init; } = clientLanguage?.ToString();

    public bool Equals(IpcMessage? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Speaker == other.Speaker && Type == other.Type && Payload == other.Payload &&
               Equals(Voice, other.Voice) && StuttersRemoved == other.StuttersRemoved && Source == other.Source &&
               NpcId == other.NpcId && ChatType == other.ChatType;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == this.GetType() && Equals((IpcMessage)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Speaker, Type, Payload, Voice, StuttersRemoved, Source, NpcId, ChatType);
    }
}