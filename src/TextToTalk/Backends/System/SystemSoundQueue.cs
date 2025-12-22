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

        private MemoryStream stream;
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly LexiconManager lexiconManager;
        //private readonly AutoResetEvent speechCompleted; //removed as the async task method takes care of threading
        private readonly StreamSoundQueue streamSoundQueue;
        private readonly SystemBackend backend;


        public Observable<SelectVoiceFailedException> SelectVoiceFailed => selectVoiceFailed;
        private readonly Subject<SelectVoiceFailedException> selectVoiceFailed;


        public async Task ASyncSpeak(SpeechSynthesizer synth, string textToSpeak) // added to work around speakssmlasync limitation for MemoryStream output
        {
           synth.SpeakSsml(textToSpeak);
        }

        public SystemSoundQueue(LexiconManager lexiconManager)
        {

            //this.stream = new MemoryStream();   // Moved this to OnSoundLoop as MemoryStream will be Disposed after processing.  Keeping it static here will cause crashes after the 1st message
            //this.speechSynthesizer.SetOutputToWaveStream(this.stream); // Paired with the above
            this.streamSoundQueue = new StreamSoundQueue(); // Opening instance to feed MemoryStream to StreamSoundQueue
            //this.speechCompleted = new AutoResetEvent(false);  // removed as the async task method takes care of threading
            this.lexiconManager = lexiconManager;
            this.speechSynthesizer = new SpeechSynthesizer();
            this.selectVoiceFailed = new Subject<SelectVoiceFailedException>();

            //this.speechSynthesizer.SpeakCompleted += (_, _) => // removed as the async task method takes care of threading
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

            this.stream = new MemoryStream();
            this.speechSynthesizer.SetOutputToWaveStream(this.stream);

            await ASyncSpeak(this.speechSynthesizer, ssml); // Wrapped Synchronous Speech Synthesis in an async Task.  This is because the SpeakAsync and SpeakSsmlAsync methods do not output a useable MemoryStream.
            
            this.stream.Seek(0, SeekOrigin.Begin);
            DetailedLog.Debug($"Stream Length = {this.stream.Length}");
            this.streamSoundQueue.EnqueueSound(stream, nextItem.Source, StreamFormat.Wave, 1f); // Hard coded 1f for volume float as ssml already takes care of user volume input




            // Waits for the AutoResetEvent lock in the callback to fire.   removed as the async task method takes care of threading
            //this.speechCompleted.WaitOne(); removed as the async task method takes care of threading

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