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

        public Observable<SelectVoiceFailedException> SelectVoiceFailed => selectVoiceFailed;
        private readonly Subject<SelectVoiceFailedException> selectVoiceFailed;


        public async Task ASyncSpeak(SpeechSynthesizer synth, string textToSpeak)
        {
            synth.SpeakSsml(textToSpeak);
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

            this.stream = new MemoryStream();
            this.speechSynthesizer.SetOutputToWaveStream(this.stream);

            await ASyncSpeak(this.speechSynthesizer,
                ssml); // Wrapped Synchronous Speech Synthesis in an async Task.  This is because the SpeakAsync and SpeakSsmlAsync methods do not output a useable MemoryStream.

            this.stream.Seek(0, SeekOrigin.Begin);
            DetailedLog.Debug($"Stream Length = {this.stream.Length}");
            this.streamSoundQueue.EnqueueSound(stream, nextItem.Source, StreamFormat.Wave,
                1f); // Hard coded 1f for volume float as ssml already takes care of user volume input
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