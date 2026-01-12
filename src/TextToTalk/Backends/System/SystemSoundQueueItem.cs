namespace TextToTalk.Backends.System
{
    public class SystemSoundQueueItem : SoundQueueItem
    {
        public VoicePreset Preset { get; set; }

        public string Text { get; set; }

        public bool Aborted { get; private set; }

        internal void Cancel()
        {
            Aborted = true;
        }
    }
}