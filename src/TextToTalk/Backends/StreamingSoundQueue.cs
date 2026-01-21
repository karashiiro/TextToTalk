using Google.Protobuf.WellKnownTypes;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Backends.System;
using TextToTalk.Lexicons;
using static TextToTalk.Backends.StreamSoundQueueItem;

namespace TextToTalk.Backends
{

    public class StreamingSoundQueue(PluginConfiguration config, LatencyTracker latencyTracker) : SoundQueue<StreamingSoundQueueItem>
    {
        // WASAPI Hardware Members
        private WasapiOut? soundOut;
        private BufferedWaveProvider? bufferedProvider;
        private VolumeSampleProvider? volumeProvider;
        private readonly object soundLock = new();

        // 1. Unified Audio Configuration
        private static readonly WaveFormat Uberduck = new(22050, 16, 1);
        private static readonly WaveFormat Wave = new(24000, 16, 1);
        private static readonly WaveFormat Mp3 = new(24000, 16, 1);
        private static readonly WaveFormat Azure = new(16000, 16, 1);

        private bool _isDisposed;
        public CancellationTokenSource? _ttsCts;

        private LatencyTracker latencyTracker = latencyTracker;

        public void EnqueueSound(Stream data, TextSource source, float volume, StreamFormat format, HttpResponseMessage? response, long? timeStamp)
        {
            AddQueueItem(new StreamingSoundQueueItem
            {
                Data = data,
                Source = source,
                Volume = volume,
                Format = format,
                Response = response,
                StartTime = timeStamp,
            });
        }

        protected override void OnSoundLoop(StreamingSoundQueueItem nextItem)
        {
            // 1. Handle Seekable vs Network Streams
            if (nextItem.Data.CanSeek)
            {
                nextItem.Data.Position = 0;
            }

            // 2. Branch logic based on format (Encoded vs Raw)
            if (nextItem.Format == StreamFormat.Mp3 || nextItem.Format == StreamFormat.Uberduck)
            {
                ProcessMp3Stream(nextItem);
            }
 
            else
            {
                ProcessRawPcmStream(nextItem);
            }
        }

        private void ProcessMp3Stream(StreamingSoundQueueItem nextItem)
        {
            IMp3FrameDecompressor decompressor = null;
            try
            {
                // Wrap the network stream to support forward-only Position tracking 
                // and prevent partial-read exceptions in LoadFromStream.
                using var readFullyStream = new ReadFullyStream(nextItem.Data);
                while (true)
                {

                    Mp3Frame frame;
                    try
                    {
                        frame = Mp3Frame.LoadFromStream(readFullyStream);
                    }
                    catch (Exception) // Catching interruptions here
                    {
                        break;
                    }

                    if (frame == null) break;

                    if (decompressor == null)
                    {
                        WaveFormat mp3Format = new Mp3WaveFormat(frame.SampleRate,
                            frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                            frame.FrameLength, frame.BitRate);

                        decompressor = new AcmMp3FrameDecompressor(mp3Format);

                        lock (this.soundLock)
                        {
                            EnsureHardwareInitialized(decompressor.OutputFormat);
                        }
                    }

                    byte[] decompressedBuffer = new byte[16384 * 2];
                    int decompressedBytes = decompressor.DecompressFrame(frame, decompressedBuffer, 0);
                    ApplyVolumeToPcmBuffer(decompressedBuffer, decompressedBytes, nextItem.Volume);

                    if (decompressedBytes > 0)
                    {
                        lock (this.soundLock)
                        {
                            if (this.bufferedProvider != null && this.soundOut != null)
                            {
                                this.bufferedProvider.AddSamples(decompressedBuffer, 0, decompressedBytes);
                                if (this.bufferedProvider.BufferedBytes > 4096 &&
                                    this.soundOut.PlaybackState != PlaybackState.Playing)
                                {
                                    if (nextItem.StartTime.HasValue)
                                    {
                                        var elapsed = Stopwatch.GetElapsedTime(nextItem.StartTime.Value);
                                        latencyTracker.AddLatency(elapsed.TotalMilliseconds);
                                        Log.Debug("Total Latency (Say -> PlayMp3): {Ms}", elapsed.TotalMilliseconds);
                                    }
                                    this.soundOut.Play();
                                }
                            }


                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during real-time ACM decompression");
            }
            finally
            {
                decompressor?.Dispose();
                nextItem.Data.Dispose();
            }
        }

        private void ProcessRawPcmStream(StreamingSoundQueueItem nextItem)
        {
            WaveFormat chunkFormat = nextItem.Format switch
            {
                StreamFormat.Wave => Wave,
                StreamFormat.Azure => Azure,
                StreamFormat.Piper => Uberduck,
                StreamFormat.PiperLow => Azure,
                StreamFormat.PiperHigh => Wave,
                _ => throw new NotSupportedException($"Format {nextItem.Format} requires a decompressor."),
            };

            lock (this.soundLock)
            {
                EnsureHardwareInitialized(chunkFormat);
                byte[] chunkBuffer = new byte[16384];
                int bytesRead;
                bool latencyLogged = false;



                while ((bytesRead = nextItem.Data.Read(chunkBuffer, 0, chunkBuffer.Length)) > 0)
                {
                    ApplyVolumeToPcmBuffer(chunkBuffer, bytesRead, nextItem.Volume);
                    this.bufferedProvider.AddSamples(chunkBuffer, 0, bytesRead);

                    if (this.bufferedProvider.BufferedBytes > 16384 && this.soundOut.PlaybackState != PlaybackState.Playing)
                    {
                        if (nextItem.StartTime.HasValue)
                        {
                            var elapsed = Stopwatch.GetElapsedTime(nextItem.StartTime.Value);
                            latencyTracker.AddLatency(elapsed.TotalMilliseconds);
                            Log.Debug("Total Latency (Say -> PlayPCM): {Ms}", elapsed.TotalMilliseconds);
                        }

                        this.soundOut.Play();
                        latencyLogged = true;
                    }
                }
            }
            nextItem.Data.Dispose();
        }


        private void EnsureHardwareInitialized(WaveFormat format)
        {
            if (this.soundOut == null || !this.bufferedProvider.WaveFormat.Equals(format))
            {
                this.StopHardware();
                this.bufferedProvider = new BufferedWaveProvider(format) { ReadFully = true };
                this.bufferedProvider.BufferDuration = TimeSpan.FromSeconds(30);

                var mmDevice = GetWasapiDeviceFromGuid(config.SelectedAudioDeviceGuid);
                this.soundOut = new WasapiOut(mmDevice, AudioClientShareMode.Shared, false, 50);
                this.soundOut.Init(this.bufferedProvider);
            }
        }

        private MMDevice GetWasapiDeviceFromGuid(Guid targetGuid)
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                if (device.Properties.Contains(PropertyKeys.PKEY_AudioEndpoint_GUID))
                {
                    var guidString = device.Properties[PropertyKeys.PKEY_AudioEndpoint_GUID].Value as string;
                    if (Guid.TryParse(guidString, out var deviceGuid) && deviceGuid == targetGuid)
                        return device;
                }
            }
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        }
        protected override void OnSoundCancelled()
        {
            StopHardware();
        }
        public override void CancelAllSounds()
        {
            StopHardware();
            base.CancelAllSounds();
        }

        public void StopHardware()
        {
            lock (this.soundLock)
            {
                if (this.soundOut != null)
                {
                    this.soundOut.Stop();
                    Thread.Sleep(10);
                    this.soundOut.Dispose();
                    this.soundOut = null;
                }
                if (this.bufferedProvider != null)
                {
                    this.bufferedProvider.ClearBuffer();
                    this.bufferedProvider = null;
                }
            }

        }
        protected override void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            _isDisposed = true; // Signal all loops to stop immediately

            if (disposing)
            {
                try
                {
                    soundOut?.Stop();
                }
                catch (Exception ex)
                {
                    DetailedLog.Error(ex, "Error during early shutdown phase");
                }

                base.Dispose(disposing); // Clean up the queue thread
                soundOut?.Dispose();
            }
        }
        private void ApplyVolumeToPcmBuffer(byte[] buffer, int bytesRead, float volume)
        {
            // Skip calculation if volume is 100% to save CPU
            if (Math.Abs(volume - 1.0f) < 0.001f) return;

            for (int i = 0; i < bytesRead; i += 2)
            {
                // 1. Combine two bytes into one 16-bit signed integer (short)
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);

                // 2. Scale the sample value by the volume float (0.0 to 1.0)
                float scaledSample = sample * volume;

                // 3. Clamp the value to ensure it stays within 16-bit bounds to prevent "pops"
                if (scaledSample > short.MaxValue) scaledSample = short.MaxValue;
                if (scaledSample < short.MinValue) scaledSample = short.MinValue;

                short finalSample = (short)scaledSample;

                // 4. Split the 16-bit sample back into two bytes and store in the buffer
                buffer[i] = (byte)(finalSample & 0xFF);
                buffer[i + 1] = (byte)((finalSample >> 8) & 0xFF);
            }
        }
    }

    public class ReadFullyStream : Stream
    {
        private readonly Stream sourceStream;
        private long pos;

        public ReadFullyStream(Stream sourceStream)
        {
            this.sourceStream = sourceStream;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            try
            {
                while (totalBytesRead < count)
                {
                    int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0) break;
                    totalBytesRead += bytesRead;
                }
            }
            catch (Exception ex) when (ex.InnerException is SocketException se && se.NativeErrorCode == 10053)
            {
                // "An established connection was aborted by the software in your host machine"
                // This happens when we cancel the TTS request. Return 0 to signal End of Stream.
                return 0;
            }
            catch (IOException)
            {
                // General network interruption during skip
                return 0;
            }

            pos += totalBytesRead;
            return totalBytesRead;
        }

        // Required for the class to compile
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        // We provide a getter for Position so Mp3Frame can track its progress,
        // but the setter is not supported.
        public override long Position
        {
            get => pos;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

}
