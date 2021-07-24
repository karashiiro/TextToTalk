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
        private readonly IList<SoundQueueItem> queuedSounds;

        private readonly Thread soundThread;
        private readonly AutoResetEvent speechCompleted;

        private SoundQueueItem currentItem;
        private WaveOut waveOut;
        private bool active;

        public SoundQueue()
        {
            this.queuedSounds = new List<SoundQueueItem>();
            this.speechCompleted = new AutoResetEvent(false);

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

                // Play the sound
                this.waveOut = new WaveOut();
                this.waveOut.PlaybackStopped += (_, _) =>
                {
                    this.speechCompleted.Set();
                };
                this.waveOut.Init(volumeSampleProvider);
                this.waveOut.Play();
                this.speechCompleted.WaitOne();

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
                    SafeRemoveAt(0);
                }
            }

            StopWaveOut();
        }

        public void CancelFromSource(TextSource source)
        {
            lock (this.queuedSounds)
            {
                for (var i = this.queuedSounds.Count - 1; i >= 0; i--)
                {
                    if (this.queuedSounds[i].Source == source)
                    {
                        SafeRemoveAt(i);
                    }
                }
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

        /// <summary>
        /// Safely remove the item at the specified index from the sound queue. Always
        /// call this method in a synchronized block.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        private void SafeRemoveAt(int index)
        {
            // We dispose after removing to avoid edge cases in which the item is disposed
            // and then pulled from the sound thread, before we remove it.

            // ReSharper disable InconsistentlySynchronizedField
            var nextItem = this.queuedSounds[index];
            this.queuedSounds.RemoveAt(index);
            nextItem.Data.Dispose();
            // ReSharper restore InconsistentlySynchronizedField
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