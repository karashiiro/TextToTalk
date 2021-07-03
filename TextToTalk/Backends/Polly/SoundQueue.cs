using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace TextToTalk.Backends.Polly
{
    public class SoundQueue : IDisposable
    {
        private readonly ConcurrentQueue<MemoryStream> queuedSounds;
        private readonly Thread soundThread;

        private WaveOut waveOut;
        private bool active;

        public SoundQueue()
        {
            this.queuedSounds = new ConcurrentQueue<MemoryStream>();
            this.active = true;
            this.soundThread = new Thread(PlaySoundLoop);
            this.soundThread.Start();
        }

        private void PlaySoundLoop()
        {
            while (active)
            {
                if (!this.queuedSounds.TryDequeue(out var nextStream))
                {
                    Thread.Sleep(100);
                    continue;
                }

                using var mp3Reader = new Mp3FileReader(nextStream);
                using var waveStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader);
                using var blockAlignmentStream = new BlockAlignReductionStream(waveStream);

                while (this.waveOut != null)
                {
                    Thread.Sleep(100);
                }
                this.waveOut = new WaveOut();

                this.waveOut.Init(blockAlignmentStream);
                this.waveOut.Play();

                while (this.waveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }

                this.waveOut.Dispose();
                this.waveOut = null;

                nextStream.Dispose();
            }
        }

        public void EnqueueSound(MemoryStream stream)
        {
            this.queuedSounds.Enqueue(stream);
        }

        public void CancelAllSounds()
        {
            while (this.queuedSounds.Count > 0)
            {
                this.queuedSounds.TryDequeue(out var nextStream);
                nextStream.Dispose();
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
    }
}