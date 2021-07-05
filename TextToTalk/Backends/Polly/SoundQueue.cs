using NAudio.Wave;
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
                using var waveStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader);
                using var blockAlignmentStream = new BlockAlignReductionStream(waveStream);

                while (this.waveOut != null)
                {
                    Thread.Sleep(100);
                }
                this.waveOut = new WaveOut
                {
                    Volume = nextItem.Volume,
                };

                this.waveOut.Init(blockAlignmentStream);
                this.waveOut.Play();

                while (this.waveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }

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