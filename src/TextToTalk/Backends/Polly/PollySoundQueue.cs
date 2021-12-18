using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading;

namespace TextToTalk.Backends.Polly
{
    public class PollySoundQueue : SoundQueue<PollySoundQueueItem>
    {
        private readonly AutoResetEvent speechCompleted;
        private WaveOut waveOut;

        public PollySoundQueue()
        {
            this.speechCompleted = new AutoResetEvent(false);
        }

        protected override void OnSoundLoop(PollySoundQueueItem nextItem)
        {
            using var mp3Reader = new Mp3FileReader(nextItem.Data);

            // Adjust the volume of the MP3 data
            var sampleProvider = mp3Reader.ToSampleProvider();
            var volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = nextItem.Volume };

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
        }

        protected override void OnSoundCancelled()
        {
            StopWaveOut();
        }

        public void EnqueueSound(MemoryStream data, TextSource source, float volume)
        {
            AddQueueItem(new PollySoundQueueItem
            {
                Data = data,
                Volume = volume,
                Source = source,
            });
        }

        private void StopWaveOut()
        {
            try
            {
                this.waveOut?.Stop();
            }
            catch (ObjectDisposedException) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.waveOut?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}