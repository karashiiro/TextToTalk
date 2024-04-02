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

        return IpcMessage.FromSayRequest(IpcMessageType.Say, request, messageTemplate, clientLanguage, stuttersRemoved);
    }

    public IpcMessage CreateCancel(TextSource source)
    {
        var stuttersRemoved = configProvider.AreStuttersRemoved();
        var request = SayRequest.Default.WithSource(source);
        return IpcMessage.FromSayRequest(IpcMessageType.Cancel, request, request.Text, null, stuttersRemoved);
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