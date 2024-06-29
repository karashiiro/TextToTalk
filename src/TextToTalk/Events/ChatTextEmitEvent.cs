﻿using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class ChatTextEmitEvent(
    SeString speaker,
    SeString text,
    IGameObject? obj,
    XivChatType chatType)
    : TextEmitEvent(TextSource.Chat, speaker, text, obj)
{
    /// <summary>
    /// The chat type of the message.
    /// </summary>
    public XivChatType ChatType { get; } = chatType;
}