using Dalamud.Game;
using TextToTalk.Backends.Piper;

namespace TextToTalk.Backends.Piper;

public class PiperSoundQueueItem : SoundQueueItem
{
    public string Text { get; }
    public PiperVoicePreset Voice { get; }

    public float Speed { get; }
    public float Volume { get; }
    public bool Aborted { get; private set; }
    public ClientLanguage Language { get; }

    public long? StartTime { get; set; }

    public PiperSoundQueueItem(string text, PiperVoicePreset voice, TextSource source, ClientLanguage language, long? startTime)
    {
        Text = text;
        Voice = voice;
        Source = source;
        Language = language;
        StartTime = startTime;

        Speed = voice.Speed ?? 1.0f;
        Volume = voice.Volume ?? 1.0f;
    }
}