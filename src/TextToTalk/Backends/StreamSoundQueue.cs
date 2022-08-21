using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading;

namespace TextToTalk.Backends
{
    public class StreamSoundQueue : SoundQueue<StreamSoundQueueItem>
    {
        private readonly AutoResetEvent speechCompleted;
        private readonly object waveLock;
        private WasapiOut waveOut;

        public StreamSoundQueue()
        {
            this.speechCompleted = new AutoResetEvent(false);
            this.waveLock = true;
        }

        protected override void OnSoundLoop(StreamSoundQueueItem nextItem)
        {
            using WaveStream reader = nextItem.Format switch
            {
                StreamFormat.Mp3 => new Mp3FileReader(nextItem.Data),
                StreamFormat.Wave => new WaveFileReader(nextItem.Data),
                _ => throw new NotSupportedException(),
            };

            // Adjust the volume of the audio data
            var sampleProvider = reader.ToSampleProvider();
            var volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = nextItem.Volume };

            // Play the sound
            lock (this.waveLock)
            {
                this.waveOut = new WasapiOut();
                this.waveOut.PlaybackStopped += (_, _) => { this.speechCompleted.Set(); };
                this.waveOut.Init(volumeSampleProvider);
                this.waveOut.Play();
            }

            this.speechCompleted.WaitOne();

            // Cleanup
            lock (this.waveLock)
            {
                this.waveOut.Dispose();
                this.waveOut = null;
            }
        }

        protected override void OnSoundCancelled()
        {
            StopWaveOut();
        }

        public void EnqueueSound(MemoryStream data, TextSource source, StreamFormat format, float volume)
        {
            AddQueueItem(new StreamSoundQueueItem
            {
                Data = data,
                Volume = volume,
                Source = source,
                Format = format,
            });
        }

        private void StopWaveOut()
        {
            try
            {
                lock (this.waveLock)
                {
                    this.waveOut?.Stop();
                }
            }
            catch (ObjectDisposedException) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopWaveOut();

                lock (this.waveLock)
                {
                    this.waveOut?.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}