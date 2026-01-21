using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using R3;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.System
{
    public class SystemSoundQueue : SoundQueue<SystemSoundQueueItem>
    {
        private WasapiOut? soundOut;
        private BufferedWaveProvider? bufferedProvider;
        private VolumeSampleProvider? volumeProvider;
        private readonly object soundLock = new();

        private static readonly WaveFormat SystemFormat = new(22050, 16, 1);
        private readonly SpeechSynthesizer _speechSynthesizer;
        private readonly LexiconManager _lexiconManager;
        private readonly PluginConfiguration _config;


        private readonly Subject<SelectVoiceFailedException> _selectVoiceFailed = new();
        public Observable<SelectVoiceFailedException> SelectVoiceFailed => _selectVoiceFailed;
        private bool _isDisposed;

        private readonly Dictionary<string, SpeechSynthesizer> _synthPool = new();

        private SpeechSynthesizer GetSynthesizerForVoice(string voiceName)
        {
            if (!_synthPool.TryGetValue(voiceName, out var synth))
            {
                synth = new SpeechSynthesizer();
                synth.SelectVoice(voiceName);
                // Pre-link the bridge for this specific synth
                _synthPool[voiceName] = synth;
            }
            return synth;
        }
        public void EnqueueSound(VoicePreset preset, TextSource source, string text, long? timeStamp)
        {
            AddQueueItem(new SystemSoundQueueItem
            {
                Preset = preset,
                Source = source,
                Text = text,
                StartTime = timeStamp,
            });
        }

        public SystemSoundQueue(LexiconManager lexiconManager, PluginConfiguration config)
        {
            _lexiconManager = lexiconManager;
            _config = config;
            _speechSynthesizer = new SpeechSynthesizer();

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

        protected override void OnSoundLoop(SystemSoundQueueItem nextItem)
        {
            if (nextItem.Preset is not SystemVoicePreset preset || nextItem.Aborted) return;

            lock (this.soundLock)
            {
                if (this.soundOut == null)
                {
                    var mmDevice = GetWasapiDeviceFromGuid(_config.SelectedAudioDeviceGuid);

                    this.bufferedProvider = new BufferedWaveProvider(new WaveFormat(22050, 16, 1))
                    {
                        ReadFully = true, 
                        BufferDuration = TimeSpan.FromSeconds(30)
                    };
                    this.soundOut = new WasapiOut(mmDevice, AudioClientShareMode.Shared, false, 50);
                    this.soundOut.Init(this.bufferedProvider);
                }
            }

            if (!_synthPool.TryGetValue(preset.VoiceName, out var synth))
            {
                synth = new SpeechSynthesizer();
                synth.SelectVoice(preset.VoiceName);
                _synthPool[preset.VoiceName] = synth;
            }

            synth.Volume = preset.Volume;
            synth.Rate = preset.Rate;

            if (_speechSynthesizer.Voice.Name != preset.VoiceName)
            {
                _speechSynthesizer.SelectVoice(preset.VoiceName);
            }
            _speechSynthesizer.Volume = preset.Volume;
            _speechSynthesizer.Rate = preset.Rate;

            var ssml = _lexiconManager.MakeSsml(nextItem.Text, langCode: _speechSynthesizer.Voice.Culture.IetfLanguageTag);

            // Start Synthesis in Background (Feeding the buffer via bridge)
            using var bridge = new SynthesisBridgeStream(this.bufferedProvider!);
            _speechSynthesizer.SetOutputToWaveStream(bridge);

            var synthPrompt = _speechSynthesizer.SpeakSsmlAsync(ssml);

            while (!nextItem.Aborted && (!synthPrompt.IsCompleted || this.bufferedProvider?.BufferedBytes > 44))
            {
                if (this.bufferedProvider?.BufferedBytes > 512) // Pre-roll threshold
                {
                    if (this.soundOut?.PlaybackState != PlaybackState.Playing)
                    {
                        if (nextItem.StartTime.HasValue)
                        {
                            var elapsed = Stopwatch.GetElapsedTime(nextItem.StartTime.Value);
                            Log.Information("Total Latency (Say -> Play): {Ms}ms", elapsed.TotalMilliseconds);
                        }
                        this.soundOut?.Play();
                    }
                }
            }

            this.StopHardware();
        }

        // Custom Stream to pipe synthesizer output directly to NAudio buffer
        private class SynthesisBridgeStream : Stream
        {
            private readonly BufferedWaveProvider _target;
            private int _bytesToSkip = 44;
            private bool _headerSkipped = false;
            private long _position = 0;

            public SynthesisBridgeStream(BufferedWaveProvider target) => _target = target;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_bytesToSkip > 0)
                {
                    int skipNow = Math.Min(count, _bytesToSkip);
                    _bytesToSkip -= skipNow;
                    offset += skipNow;
                    count -= skipNow;
                }

                if (count > 0)
                {
                    _target.AddSamples(buffer, offset, count);
                }
            }

            public override bool CanRead => false;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _position;
            public override long Position { get => _position; set => _position = value; }

            public override long Seek(long offset, SeekOrigin origin) => _position; // Dummy seek
            public override void SetLength(long value) { }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
        }

        protected override void OnSoundCancelled()
        {
            GetCurrentItem()?.Cancel();

            _speechSynthesizer.SpeakAsyncCancelAll();

            StopHardware();
        }

        public override void CancelAllSounds()
        {
            if (_isDisposed) return;

            try
            {
                _speechSynthesizer?.SpeakAsyncCancelAll();
            }
            catch (ObjectDisposedException) { }

            StopHardware();

            base.CancelAllSounds();
        }

        private void StopHardware()
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
            _isDisposed = true;

            if (disposing)
            {
                try
                {
                    soundOut?.Stop();

                    _speechSynthesizer?.SpeakAsyncCancelAll();
                    _speechSynthesizer?.SetOutputToNull();

                }
                catch (Exception ex)
                {
                    DetailedLog.Error(ex, "Error during early shutdown phase");
                }

                base.Dispose(disposing); 

                _speechSynthesizer?.Dispose();
                soundOut?.Dispose();
            }
        }
    }
}