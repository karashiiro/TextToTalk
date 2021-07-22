using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;

namespace TextToTalk.Backends.System
{
    public class SoundQueue : IDisposable
    {
        // This used to be a ConcurrentQueue<T>, but was changed so that items could be
        // queried and removed from the middle.
        private IList<SoundQueueItem> queuedSounds;

        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly Thread soundThread;

        private bool active;

        public SoundQueue()
        {
            this.queuedSounds = new List<SoundQueueItem>();
            this.active = true;
            this.soundThread = new Thread(PlaySoundLoop);
            this.soundThread.Start();

            this.speechSynthesizer = new SpeechSynthesizer();
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

        private void PlaySoundLoop()
        {
            while (active)
            {
                var nextItem = TryDequeue();
                if (nextItem == null)
                {
                    Thread.Sleep(100);
                    continue;
                }

                this.speechSynthesizer.UseVoicePreset(nextItem.Preset);
                this.speechSynthesizer.SpeakAsync(nextItem.Text);
            }
        }

        public void EnqueueSound(VoicePreset preset, TextSource source, string text)
        {
            lock (this.queuedSounds)
            {
                this.queuedSounds.Add(new SoundQueueItem
                {
                    Preset = preset,
                    Text = text,
                    Source = source,
                });
            }
        }

        public void CancelAllSounds()
        {
            lock (this.queuedSounds)
            {
                while (this.queuedSounds.Count > 0)
                {
                    this.queuedSounds.RemoveAt(0);
                }
            }

            this.speechSynthesizer.SpeakAsyncCancelAll();
        }

        public void CancelFromSource(TextSource source)
        {
            lock (this.queuedSounds)
            {
                this.queuedSounds = this.queuedSounds.Where(s => s.Source == source).ToList();
            }
        }

        private SoundQueueItem TryDequeue()
        {
            SoundQueueItem nextItem;
            lock (this.queuedSounds)
            {
                nextItem = this.queuedSounds.FirstOrDefault();
                if (nextItem != null)
                {
                    this.queuedSounds.RemoveAt(0);
                }
            }

            return nextItem;
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

            public TextSource Source { get; set; }
        }
    }
}