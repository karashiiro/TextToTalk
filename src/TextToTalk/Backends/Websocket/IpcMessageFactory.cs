using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using TextToTalk.Utils;

namespace TextToTalk.Backends.Websocket;

public class IpcMessageFactory(IClientState clientState, IWebsocketConfigProvider configProvider) : IIpcMessageFactory
{
    public IpcMessage CreateBroadcast(string speaker, TextSource source, VoicePreset voice, string message, uint? npcId,
        XivChatType? chatType)
    {
        var clientLanguage = clientState.ClientLanguage;
        var stuttersRemoved = configProvider.AreStuttersRemoved();
        var messageTemplate = TalkUtils.ExtractTokens(message, new Dictionary<string, string?>
        {
            { "{{FULL_NAME}}", GetLocalFullName() },
            { "{{FIRST_NAME}}", GetLocalFirstName() },
            { "{{LAST_NAME}}", GetLocalLastName() },
        });

        return new IpcMessage(speaker, IpcMessageType.Say, message, messageTemplate, voice, source, clientLanguage,
            stuttersRemoved, npcId, chatType);
    }

    public IpcMessage CreateCancel(TextSource source)
    {
        var stuttersRemoved = configProvider.AreStuttersRemoved();
        return new IpcMessage(string.Empty, IpcMessageType.Cancel, string.Empty, string.Empty, null, source, null,
            stuttersRemoved, null, null);
    }

    private string? GetLocalFullName()
    {
        return clientState.LocalPlayer?.Name.TextValue;
    }

    private string? GetLocalFirstName()
    {
        var parts = GetLocalFullName()?.Split(" ");
        return parts is [{ } firstName, not null] ? firstName : null;
    }

    private string? GetLocalLastName()
    {
        var parts = GetLocalFullName()?.Split(" ");
        return parts is [not null, { } lastName] ? lastName : null;
    }
}