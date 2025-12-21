//using Microsoft.CognitiveServices.Speech;
using NAudio.SoundFont;
using NAudio.Wave;
using R3;
using Serilog;
using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Lexicons;
using static Google.Rpc.Context.AttributeContext.Types;

namespace TextToTalk.Backends.System
{
    

    public class SystemSoundQueue : SoundQueue<SystemSoundQueueItem>
    {

        private readonly MemoryStream stream;
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly LexiconManager lexiconManager;
        private readonly AutoResetEvent speechCompleted;
        private readonly StreamSoundQueue streamSoundQueue;
        private readonly SystemBackend backend;


        public Observable<SelectVoiceFailedException> SelectVoiceFailed => selectVoiceFailed;
        private readonly Subject<SelectVoiceFailedException> selectVoiceFailed;

        public async Task PlayWaveStream(Stream waveStream)
        {
            // NAudio uses IWaveProvider or similar interfaces to read from streams
            using (var waveReader = new WaveFileReader(waveStream))
            {
                // Use WaveOutEvent for playback, as it runs on a separate thread and is non-blocking
                using (var outputDevice = new WaveOutEvent())
                {
                    outputDevice.DeviceNumber = SelectedAudioDevice.selectedAudioDeviceIndex;
                    outputDevice.Init(waveReader);
                    Log.Information("Playing via Narrator");
                    outputDevice.Play();

                    // Wait for playback to complete (can be adapted for full async/await using events)
                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        await Task.Delay(100);
                    }
                }
            }
        }

        public async Task ASyncSpeak(SpeechSynthesizer synth, string textToSpeak)
        {
           synth.SpeakSsml(textToSpeak);
        }

        public SystemSoundQueue(LexiconManager lexiconManager)
        {
            this.stream = new MemoryStream();
            //this.speechCompleted = new AutoResetEvent(false);
            this.lexiconManager = lexiconManager;
            this.speechSynthesizer = new SpeechSynthesizer();
            this.speechSynthesizer.SetOutputToWaveStream(this.stream);

            this.selectVoiceFailed = new Subject<SelectVoiceFailedException>();

            //this.speechSynthesizer.SpeakCompleted += (_, _) =>
            //{
                // Allows PlaySoundLoop to continue.
                //this.speechCompleted.Set();
            //};
        }

        public void EnqueueSound(VoicePreset preset, TextSource source, string text)
        {
            AddQueueItem(new SystemSoundQueueItem
            {
                Preset = preset,
                Text = text,
                Source = source,
            });
        }

        protected override async void OnSoundLoop(SystemSoundQueueItem nextItem)
        {
            if (nextItem.Preset is not SystemVoicePreset systemVoicePreset)
            {
                throw new InvalidOperationException("Invalid voice preset provided.");
            }

            try
            {
                this.speechSynthesizer.UseVoicePreset(nextItem.Preset);
            }
            catch (SelectVoiceFailedException e)
            {
                DetailedLog.Error(e, "Failed to select voice {0}", systemVoicePreset.VoiceName ?? "");
                this.selectVoiceFailed.OnNext(e);
            }

            var ssml = this.lexiconManager.MakeSsml(nextItem.Text,
                langCode: this.speechSynthesizer.Voice.Culture.IetfLanguageTag);
            DetailedLog.Verbose(ssml);

            await ASyncSpeak(this.speechSynthesizer, ssml);
            this.stream.Seek(0, SeekOrigin.Begin);
            Log.Information($"Stream Length = {this.stream.Length}");
            //IT KEEPS CRASHING HERE.  WHY?????
            await PlayWaveStream(this.stream);
            this.stream.SetLength(0);
            


            // Waits for the AutoResetEvent lock in the callback to fire.
            //streamSoundQueue.EnqueueSound(stream, nextItem.Source, StreamFormat.Raw, 1f);
            //this.speechCompleted.WaitOne();
            //this.stream.Dispose();
            //this.speechSynthesizer.Dispose();

        }

        protected override void OnSoundCancelled()
        {
            try
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.speechSynthesizer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}