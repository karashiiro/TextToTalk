using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading;


namespace TextToTalk.Backends
{
    public class StreamSoundQueue(PluginConfiguration config) : SoundQueue<StreamSoundQueueItem>
    {
        private static readonly WaveFormat waveFormat = new(24000, 16, 1);
        private readonly AutoResetEvent speechCompleted = new(false);
        private readonly object soundLock = true;
        private DirectSoundOut? soundOut;
        private volatile bool cancelled;

        protected override void OnSoundLoop(StreamSoundQueueItem nextItem)
        {
            using WaveStream reader = nextItem.Format switch
            {
                StreamFormat.Mp3 => new Mp3FileReader(nextItem.Data),
                StreamFormat.Wave => new WaveFileReader(nextItem.Data),
                StreamFormat.Raw => new RawSourceWaveStream(nextItem.Data, waveFormat),
                _ => throw new NotSupportedException(),
            };

            // Adjust the volume of the audio data. The per-item volume comes from the
            // backend's voice preset (or 1.0 for the System backend, which applies its
            // preset volume natively via SAPI before reaching this point); the global
            // multiplier is applied uniformly here so every backend honors it consistently.
            var sampleProvider = reader.ToSampleProvider();
            var effectiveVolume = nextItem.Volume * config.GlobalVolume;
            var volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = effectiveVolume };
            var playbackDeviceId = config.SelectedAudioDeviceGuid;

            this.cancelled = false;

            // Play the sound
            lock (this.soundLock)
            {
                this.soundOut = new DirectSoundOut(playbackDeviceId);
                this.soundOut.PlaybackStopped += (_, _) => { this.speechCompleted.Set(); };
                this.soundOut.Init(volumeSampleProvider);
                this.soundOut.Play();
            }

            this.speechCompleted.WaitOne();

            // Cleanup
            lock (this.soundLock)
            {
                this.soundOut?.Dispose();
                this.soundOut = null;
            }

            if (!this.cancelled)
            {
                SpeechFinishedNotifier.Trigger(nextItem.Source, nextItem.Text);
            }
        }

        protected override void OnSoundCancelled()
        {
            StopWaveOut();
        }

        public void EnqueueSound(MemoryStream data, TextSource source, StreamFormat format, float volume, string? text = null)
        {
            AddQueueItem(new StreamSoundQueueItem
            {
                Data = data,
                Volume = volume,
                Source = source,
                Format = format,
                Text = text,
            });
        }

        private void StopWaveOut()
        {
            try
            {
                lock (this.soundLock)
                {
                    this.cancelled = true;
                    this.soundOut?.Stop();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopWaveOut();

                lock (this.soundLock)
                {
                    this.soundOut?.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}