using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class TextEmitEvent : SourcedTextEvent
{
    public SeString SpeakerName { get; }

    public SeString Text { get; }
    
    public GameObject? Speaker { get; }

    public TextEmitEvent(TextSource source, SeString speaker, SeString text, GameObject? speakerObj) : base(source)
    {
        Speaker = speakerObj;
        SpeakerName = speaker;
        Text = text;
    }
}