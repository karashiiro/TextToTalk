﻿using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class ChatTextEmitEvent(
    TextSource source,
    SeString speaker,
    SeString text,
    GameObject? obj,
    XivChatType chatType)
    : TextEmitEvent(source, speaker, text, obj)
{
    /// <summary>
    /// The chat type of the message.
    /// </summary>
    public XivChatType ChatType { get; } = chatType;
}