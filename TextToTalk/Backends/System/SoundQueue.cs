using System;
using System.Collections.Concurrent;
using System.Speech.Synthesis;
using System.Threading;

namespace TextToTalk.Backends.System
{
    public class SoundQueue : IDisposable
    {
        private readonly ConcurrentQueue<SoundQueueItem> queuedSounds;
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly Thread soundThread;

        private bool active;

        public SoundQueue()
        {
            this.queuedSounds = new ConcurrentQueue<SoundQueueItem>();
            this.active = true;
            this.soundThread = new Thread(PlaySoundLoop);
            this.soundThread.Start();

            this.speechSynthesizer = new SpeechSynthesizer();
        }

        private void PlaySoundLoop()
        {
            while (active)
            {
                if (!this.queuedSounds.TryDequeue(out var nextItem))
                {
                    Thread.Sleep(100);
                    continue;
                }

                this.speechSynthesizer.UseVoicePreset(nextItem.Preset);
                this.speechSynthesizer.SpeakAsync(nextItem.Text);
            }
        }

        public void EnqueueSound(VoicePreset preset, string text)
        {
            this.queuedSounds.Enqueue(new SoundQueueItem
            {
                Preset = preset,
                Text = text,
            });
        }

        public void CancelAllSounds()
        {
            while (this.queuedSounds.Count > 0)
            {
                this.queuedSounds.TryDequeue(out _);
            }

            this.speechSynthesizer.SpeakAsyncCancelAll();
        }

        public void Dispose()
        {
            this.active = false;
            this.soundThread.Join();
            CancelAllSounds();

            this.speechSynthesizer.Dispose();
        }

        private class SoundQueueItem
        {
            public VoicePreset Preset { get; set; }

            public string Text { get; set; }
        }
    }
}