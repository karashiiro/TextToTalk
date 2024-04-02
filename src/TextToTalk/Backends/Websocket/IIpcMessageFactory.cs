using Dalamud.Game.Text;

namespace TextToTalk.Backends.Websocket;

public interface IIpcMessageFactory
{
    IpcMessage CreateBroadcast(string speaker, TextSource source, VoicePreset voice, string message, uint? npcId,
        XivChatType? chatType);

    IpcMessage CreateCancel(TextSource source);
}