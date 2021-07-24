using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;

namespace TextToTalk.Backends.System
{
    public class SystemSoundQueue : SoundQueue<SystemSoundQueueItem>
    {
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly AutoResetEvent speechCompleted;

        public SystemSoundQueue()
        {
            this.speechCompleted = new AutoResetEvent(false);
            this.speechSynthesizer = new SpeechSynthesizer();

            this.speechSynthesizer.SpeakCompleted += (_, _) =>
            {
                // Allows PlaySoundLoop to continue.
                this.speechCompleted.Set();
            };
        }

        public void AddLexicon(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Lexicon file does not exist.", Path.GetFileName(filePath));
            this.speechSynthesizer.AddLexicon(new Uri(filePath), "application/pls+xml");
        }

        public void RemoveLexicon(string filePath)
        {
            this.speechSynthesizer.RemoveLexicon(new Uri(filePath));
        }

        public void EnqueueSound(VoicePreset preset, TextSource source, string text)
        {
            AddQueueItem(new SystemSoundQueueItem
            {
                Preset = preset,
                Text = text,
                Source = source,
            });
        }

        protected override void OnSoundLoop(SystemSoundQueueItem nextItem)
        {
            this.speechSynthesizer.UseVoicePreset(nextItem.Preset);
            this.speechSynthesizer.SpeakAsync(nextItem.Text);

            // Waits for the AutoResetEvent lock in the callback to fire.
            this.speechCompleted.WaitOne();
        }

        protected override void OnSoundCancelled()
        {
            try
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
            }
            catch (ObjectDisposedException) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.speechSynthesizer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}