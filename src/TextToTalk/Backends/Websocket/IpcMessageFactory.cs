using System.Collections.Generic;
using Dalamud.Plugin.Services;
using TextToTalk.Utils;

namespace TextToTalk.Backends.Websocket;

public class IpcMessageFactory(IClientState clientState, IWebsocketConfigProvider configProvider) : IIpcMessageFactory
{
    public IpcMessage CreateBroadcast(SayRequest request)
    {
        var clientLanguage = clientState.ClientLanguage;
        var stuttersRemoved = configProvider.AreStuttersRemoved();
        var messageTemplate = TalkUtils.ExtractTokens(request.Text, new Dictionary<string, string?>
        {
            { "{{FULL_NAME}}", GetLocalFullName() },
            { "{{FIRST_NAME}}", GetLocalFirstName() },
            { "{{LAST_NAME}}", GetLocalLastName() },
        });

        return new IpcMessage(request.Speaker, IpcMessageType.Say, request.Text, messageTemplate, request.Voice,
            request.Source, clientLanguage, stuttersRemoved, request.NpcId, request.ChatType);
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