using FFXIVClientStructs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using TextToTalk.UI;
using TextToTalk;

namespace TextToTalk.Backends
{
    public static class AudioDevices
    {
        public static IEnumerable<DirectSoundDeviceInfo> deviceList = DirectSoundOut.Devices;
        
    }
   
    

    public class StreamSoundQueue : SoundQueue<StreamSoundQueueItem>
    {

        private static readonly WaveFormat waveFormat = new(24000, 16, 1);
        private readonly AutoResetEvent speechCompleted;
        private readonly object soundLock;
        private DirectSoundOut? soundOut;



        public StreamSoundQueue()
        {
            this.speechCompleted = new AutoResetEvent(false);
            this.soundLock = true;
            
            
            
        }

        protected override void OnSoundLoop(StreamSoundQueueItem nextItem)
        {
        
            using WaveStream reader = nextItem.Format switch
            {
                StreamFormat.Mp3 => new Mp3FileReader(nextItem.Data),
                StreamFormat.Wave => new WaveFileReader(nextItem.Data),
                StreamFormat.Raw => new RawSourceWaveStream(nextItem.Data, waveFormat),
                _ => throw new NotSupportedException(),
            };

            // Adjust the volume of the audio data
            
            var sampleProvider = reader.ToSampleProvider();
            var volumeSampleProvider = new VolumeSampleProvider(sampleProvider) { Volume = nextItem.Volume };
            

            // Play the sound

            lock (this.soundLock)
            {
                var deviceGuid = SelectedAudioDevice.selectedAudioDevice;
                DetailedLog.Info($"Selected Audio Device: {deviceGuid}");
                this.soundOut = new DirectSoundOut(deviceGuid);
                this.soundOut.PlaybackStopped += (_, _) => { this.speechCompleted.Set(); };
                this.soundOut.Init(volumeSampleProvider);
                this.soundOut.Play();
            }

            this.speechCompleted.WaitOne();

            // Cleanup
            lock (this.soundLock)
            {
                this.soundOut.Dispose();
                this.soundOut = null;
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
                lock (this.soundLock)
                {
                    this.soundOut?.Stop();
                }
            }
            catch (ObjectDisposedException) { }
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