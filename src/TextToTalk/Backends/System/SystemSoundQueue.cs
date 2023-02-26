using System;
using System.Reactive.Subjects;
using System.Speech.Synthesis;
using System.Threading;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.System
{
    public class SystemSoundQueue : SoundQueue<SystemSoundQueueItem>
    {
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly LexiconManager lexiconManager;
        private readonly AutoResetEvent speechCompleted;

        public IObservable<SelectVoiceFailedException> SelectVoiceFailed => selectVoiceFailed;
        private readonly Subject<SelectVoiceFailedException> selectVoiceFailed;

        public SystemSoundQueue(LexiconManager lexiconManager)
        {
            this.speechCompleted = new AutoResetEvent(false);
            this.lexiconManager = lexiconManager;
            this.speechSynthesizer = new SpeechSynthesizer();
            this.speechSynthesizer.SetOutputToDefaultAudioDevice();

            this.selectVoiceFailed = new Subject<SelectVoiceFailedException>();

            this.speechSynthesizer.SpeakCompleted += (_, _) =>
            {
                // Allows PlaySoundLoop to continue.
                this.speechCompleted.Set();
            };
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
            if (nextItem.Preset is not SystemVoicePreset systemVoicePreset)
            {
                throw new InvalidOperationException("Invalid voice preset provided.");
            }

            try
            {
                this.speechSynthesizer.UseVoicePreset(nextItem.Preset);
            }
            catch (SelectVoiceFailedException e)
            {
                DetailedLog.Error(e, "Failed to select voice {0}", systemVoicePreset.VoiceName ?? "");
                this.selectVoiceFailed.OnNext(e);
            }

            var ssml = this.lexiconManager.MakeSsml(nextItem.Text,
                langCode: this.speechSynthesizer.Voice.Culture.IetfLanguageTag);
            DetailedLog.Info(ssml);

            this.speechSynthesizer.SpeakSsmlAsync(ssml);

            // Waits for the AutoResetEvent lock in the callback to fire.
            this.speechCompleted.WaitOne();
        }

        protected override void OnSoundCancelled()
        {
            try
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
            }
            catch (ObjectDisposedException)
            {
            }
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