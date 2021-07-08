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

                // Adjust the volume of the MP3 data
                {
                    // Write out the stream data to a buffer
                    var buffer = new byte[mp3Reader.WaveFormat.SampleRate];
                    int read;
                    do
                    {
                        read = mp3Reader.Read(buffer, 0, buffer.Length);
                    } while (read > 0);

                    // Scale the data
                    mp3Reader.Seek(0, SeekOrigin.Begin);
                    for (var n = 0; n < buffer.Length; n++)
                    {
                        buffer[n] = (byte)(buffer[n] * nextItem.Volume);
                    }

                    // Write the data back to the stream
                    mp3Reader.Write(buffer, 0, buffer.Length);
                    mp3Reader.Seek(0, SeekOrigin.Begin);
                }

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