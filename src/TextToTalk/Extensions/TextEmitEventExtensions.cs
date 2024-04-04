using Dalamud.Game.Text;
using TextToTalk.Events;

namespace TextToTalk.Extensions;

public static class TextEmitEventExtensions
{
    public static XivChatType? GetChatType(this TextEmitEvent ev)
    {
        if (ev is ChatTextEmitEvent chatEv)
        {
            return chatEv.ChatType;
        }

        return null;
    }
}