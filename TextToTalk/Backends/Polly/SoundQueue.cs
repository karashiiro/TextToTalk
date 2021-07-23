using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TextToTalk.Backends.Polly
{
    public class SoundQueue : IDisposable
    {
        // This used to be a ConcurrentQueue<T>, but was changed so that items could be
        // queried and removed from the middle.
        private IList<SoundQueueItem> queuedSounds;

        private readonly Thread soundThread;

        private SoundQueueItem currentItem;
        private WaveOut waveOut;
        private bool active;

        public SoundQueue()
        {
            this.queuedSounds = new List<SoundQueueItem>();
            this.active = true;
            this.soundThread = new Thread(PlaySoundLoop);
            this.soundThread.Start();
        }

        private void PlaySoundLoop()
        {
            while (active)
            {
                this.currentItem = TryDequeue();
                if (this.currentItem == null)
                {
                    Thread.Sleep(100);
                    continue;
                }

                using var mp3Reader = new Mp3FileReader(this.currentItem.Data);

                // Adjust the volume of the MP3 data
                var sampleProvider = mp3Reader.ToSampleProvider();
                var volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = this.currentItem.Volume };

                // Wait for the last sound to stop
                while (this.waveOut != null)
                {
                    Thread.Sleep(100);
                }

                // Play the sound
                this.waveOut = new WaveOut();
                this.waveOut.Init(volumeSampleProvider);
                this.waveOut.Play();

                while (this.waveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }

                // Cleanup
                this.waveOut.Dispose();
                this.waveOut = null;

                this.currentItem.Data.Dispose();
                this.currentItem = null;
            }
        }

        public void EnqueueSound(MemoryStream data, TextSource source, float volume)
        {
            lock (this.queuedSounds)
            {
                this.queuedSounds.Add(new SoundQueueItem
                {
                    Data = data,
                    Volume = volume,
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
                    var nextItem = this.queuedSounds[0];
                    this.queuedSounds.RemoveAt(0);
                    nextItem.Data.Dispose();
                }
            }

            StopWaveOut();
        }

        public void CancelFromSource(TextSource source)
        {
            lock (this.queuedSounds)
            {
                foreach (var item in this.queuedSounds.Where(s => s.Source != source).ToList())
                {
                    item.Data.Dispose();
                }

                this.queuedSounds = this.queuedSounds.Where(s => s.Source == source).ToList();
            }

            if (this.currentItem?.Source == source)
            {
                StopWaveOut();
            }
        }

        public TextSource GetCurrentlySpokenTextSource()
        {
            return this.currentItem?.Source ?? TextSource.None;
        }

        private void StopWaveOut()
        {
            try
            {
                this.waveOut?.Stop();
            }
            catch (ObjectDisposedException) { }
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
            CancelAllSounds();
            this.soundThread.Join();
            this.waveOut?.Dispose();
        }

        private class SoundQueueItem
        {
            public MemoryStream Data { get; set; }

            public float Volume { get; set; }

            public TextSource Source { get; set; }
        }
    }
}