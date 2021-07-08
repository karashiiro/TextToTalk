using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace TextToTalk.Backends.Polly
{
    public class SoundQueue : IDisposable
    {
        private readonly ConcurrentQueue<SoundQueueItem> queuedSounds;
        private readonly Thread soundThread;

        private WaveOut waveOut;
        private bool active;

        public SoundQueue()
        {
            this.queuedSounds = new ConcurrentQueue<SoundQueueItem>();
            this.active = true;
            this.soundThread = new Thread(PlaySoundLoop);
            this.soundThread.Start();
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

                using var mp3Reader = new Mp3FileReader(nextItem.Data);

                // Adjust the volume of the MP3 data
                var sampleProvider = mp3Reader.ToSampleProvider();
                var volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = nextItem.Volume };

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

                nextItem.Data.Dispose();
            }
        }

        public void EnqueueSound(MemoryStream data, float volume)
        {
            this.queuedSounds.Enqueue(new SoundQueueItem
            {
                Data = data,
                Volume = volume,
            });
        }

        public void CancelAllSounds()
        {
            while (this.queuedSounds.Count > 0)
            {
                this.queuedSounds.TryDequeue(out var nextItem);
                nextItem.Data.Dispose();
            }
            StopWaveOut();
        }

        private void StopWaveOut()
        {
            try
            {
                this.waveOut?.Stop();
            }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            this.active = false;
            this.soundThread.Join();
            CancelAllSounds();
            this.waveOut?.Dispose();
        }

        private class SoundQueueItem
        {
            public MemoryStream Data { get; set; }

            public float Volume { get; set; }
        }
    }
}