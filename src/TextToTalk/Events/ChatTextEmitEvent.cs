using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class ChatTextEmitEvent : TextEmitEvent
{
    public XivChatType ChatType { get; }

    public SeString RichSpeakerName { get; }

    public SeString RichText { get; }

    public ChatTextEmitEvent(
        TextSource source,
        SeString speaker,
        SeString text,
        XivChatType chatType) : base(source, speaker.TextValue, text.TextValue)
    {
        ChatType = chatType;
        RichSpeakerName = speaker;
        RichText = text;
    }
}