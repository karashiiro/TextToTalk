namespace TextToTalk.Backends.Websocket;

public interface IIpcMessageFactory
{
    IpcMessage CreateBroadcast(SayRequest request);

    IpcMessage CreateCancel(TextSource source);
}