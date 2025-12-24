namespace TextToTalk.Backends.System
{
    public class SystemSoundQueueItem : SoundQueueItem
    {
        public VoicePreset Preset { get; set; }

        public string Text { get; set; }
    }
}