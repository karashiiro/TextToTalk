using Riok.Mapperly.Abstractions;

namespace TextToTalk.Backends.Websocket;

[Mapper]
public partial class IpcMessageMapper
{
    [MapProperty(nameof(SayRequest.Text), nameof(IpcMessage.Payload))]
    [MapProperty(nameof(SayRequest.TextTemplate), nameof(IpcMessage.PayloadTemplate))]
    [MapProperty(nameof(SayRequest.MessageType), "type")]
    public partial IpcMessage MapSayRequest(SayRequest request);
}