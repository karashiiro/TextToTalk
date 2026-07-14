namespace TextToTalk.Backends.Megaphone;

public class MegaphoneVoicePreset : VoicePreset
{
    public override bool TrySetDefaultValues()
    {
        EnabledBackend = TTSBackend.Megaphone;
        return true;
    }
}