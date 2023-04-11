using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class ChatTextEmitEvent : TextEmitEvent
{
    public XivChatType ChatType { get; }

    public ChatTextEmitEvent(
        TextSource source,
        SeString speaker,
        SeString text,
        GameObject? obj,
        XivChatType chatType) : base(source, speaker, text, obj)
    {
        ChatType = chatType;
    }
}