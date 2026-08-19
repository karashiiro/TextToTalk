using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public abstract class TextEmitEvent(
    TextSource source,
    SeString speaker,
    SeString text,
    IGameObject? speakerObj,
    string? rawText = null)
    : SourcedTextEvent(source)
{
    /// <summary>
    /// The speaker's name. This should be considered "clean" for the purposes of
    /// portable comparison.
    /// </summary>
    public SeString SpeakerName { get; } = speaker;

    /// <summary>
    /// The expanded text from the game before TextToTalk normalization or text replacement.
    /// </summary>
    public string RawText { get; } = rawText ?? text.TextValue;

    /// <summary>
    /// The text being emitted.
    /// </summary>
    public SeString Text { get; } = text;

    /// <summary>
    /// The speaking entity, if detected.
    /// </summary>
    public IGameObject? Speaker { get; } = speakerObj;

    /// <summary>
    /// Returns if this event instance is equivalent to another.
    /// </summary>
    /// <param name="other"></param>
    /// <returns>true if the instances are equivalent; otherwise false.</returns>
    public bool IsEquivalent(TextEmitEvent? other)
    {
        return SpeakerName.TextValue == other?.SpeakerName.TextValue && Text.TextValue == other.Text.TextValue;
    }
}
