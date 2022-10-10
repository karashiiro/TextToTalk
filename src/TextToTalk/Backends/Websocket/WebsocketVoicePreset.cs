namespace TextToTalk.Backends.Websocket;

public class WebsocketVoicePreset : VoicePreset
{
    public override bool TrySetDefaultValues()
    {
        EnabledBackend = TTSBackend.Websocket;
        return true;
    }
}