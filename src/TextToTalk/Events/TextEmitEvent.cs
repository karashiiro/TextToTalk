using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class TextEmitEvent : SourcedTextEvent
{
    /// <summary>
    /// The speaker's name. This should be considered "clean" for the purposes of
    /// portable comparison.
    /// </summary>
    public SeString SpeakerName { get; }

    /// <summary>
    /// The text being emitted.
    /// </summary>
    public SeString Text { get; }
    
    /// <summary>
    /// The speaking entity, if detected.
    /// </summary>
    public GameObject? Speaker { get; }

    public TextEmitEvent(TextSource source, SeString speaker, SeString text, GameObject? speakerObj) : base(source)
    {
        Speaker = speakerObj;
        SpeakerName = speaker;
        Text = text;
    }
}