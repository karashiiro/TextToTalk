namespace TextToTalk.Backends.Uberduck;

public class UberduckVoicePreset : VoicePreset
{
    public float Volume { get; set; }

    public int Rate { get; set; }

    public string VoiceName { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        Rate = 100;
        VoiceName = "zwf";
        EnabledBackend = TTSBackend.Uberduck;
        return true;
    }
}