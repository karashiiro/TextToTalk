using R3;
using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.System
{
    public class SystemSoundQueue : SoundQueue<SystemSoundQueueItem>
    {
        private MemoryStream stream;
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly LexiconManager lexiconManager;
        private readonly StreamSoundQueue streamSoundQueue;
        private readonly SystemBackend backend;
        private readonly PluginConfiguration config;
        private int soundLock;
        private readonly SemaphoreSlim deviceLock = new SemaphoreSlim(1, 1);

        public Observable<SelectVoiceFailedException> SelectVoiceFailed => selectVoiceFailed;
        private readonly Subject<SelectVoiceFailedException> selectVoiceFailed;
        private bool isSynthesizing = false;


        public async void ASyncSpeak(SpeechSynthesizer synth, string textToSpeak)
        {
            await Task.Run(() => synth.SpeakSsml(textToSpeak));
        }

        public SystemSoundQueue(LexiconManager lexiconManager, PluginConfiguration config)
        {
            this.streamSoundQueue = new StreamSoundQueue(config);
            this.lexiconManager = lexiconManager;
            this.speechSynthesizer = new SpeechSynthesizer();
            this.selectVoiceFailed = new Subject<SelectVoiceFailedException>();
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

            try
            {
                isSynthesizing = true;

                await deviceLock.WaitAsync();

                this.stream = new MemoryStream();
                this.speechSynthesizer.SetOutputToWaveStream(this.stream);

                await Task.Run(() => this.speechSynthesizer.SpeakSsml(ssml));

            }
            catch (OperationCanceledException)
            {

            }

            finally
            {
                isSynthesizing = false;
                deviceLock.Release();
            }

            this.stream.Seek(0, SeekOrigin.Begin);
            this.streamSoundQueue.EnqueueSound(stream, nextItem.Source, StreamFormat.Wave, 1f);
        }

        public override void CancelAllSounds()
        {
            base.CancelAllSounds();
            this.streamSoundQueue.CancelAllSounds();
        }

        public override void CancelFromSource(TextSource source)
        {
            base.CancelFromSource(source);
            this.streamSoundQueue.CancelFromSource(source);
        }


        protected override void OnSoundCancelled()
        {
            try 
            {
                this.speechSynthesizer.SetOutputToNull();
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