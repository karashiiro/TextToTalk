using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using static Google.Rpc.Context.AttributeContext.Types;

namespace TextToTalk.Backends.System
{
    //public class SpeechNaudioBridge
    //{
    //    public static async Task NarratorTTS(string textToSpeak)
    //    {
    //        // Use a MemoryStream to hold the synthesized audio data temporarily
    //        using (var stream = new MemoryStream())
    //        {
    //            // Synthesize the speech into the MemoryStream in a separate task
    //            Log.Information("Starting Speech Synthesis");
    //            await Task.Run(() =>
    //
    //            {
    //                using (SpeechSynthesizer synth = new SpeechSynthesizer())
    //                {
    //                    Log.Information("created speech task");
    //                    // Configure the SpeechSynthesizer to output to our stream
    //                    synth.SetOutputToWaveStream(stream);
    //                    Log.Information("set output stream");
    //                    // Use a synchronous speak method because the whole process is wrapped in a Task.Run
    //                    synth.SpeakSsml(textToSpeak);
    //                    Log.Information($"speaking lines: {textToSpeak}");
    //                    // Ensure the stream is reset to the beginning for NAudio to read it
    //                    stream.Seek(0, SeekOrigin.Begin);
    //                    Log.Information("setting origin");
    //                }
    //            });
    //
    //            // Play the synthesized audio stream using NAudio
    //            Log.Information("playing via NAudio");
    //            SystemBackend.PlayWaveStream(stream);
    //            
    //        }
    //    }
    //}

        public static class AudioDevices
    {
        public static IEnumerable<DirectSoundDeviceInfo> deviceList = DirectSoundOut.Devices;

    }
    public class SystemBackend : VoiceBackend
    {
        private readonly SystemBackendUIModel uiModel;
        private readonly SystemBackendUI ui;
        private readonly SystemSoundQueue soundQueue;
        private readonly StreamSoundQueue streamSoundQueue;
        private DirectSoundOut? soundOut;
        private readonly AutoResetEvent speechCompleted;

        private readonly IDisposable voiceExceptions;

        public SystemBackend(PluginConfiguration config, HttpClient http)
        {
            var lexiconManager = new DalamudLexiconManager();
            LexiconUtils.LoadFromConfigSystem(lexiconManager, config);

            this.uiModel = new SystemBackendUIModel();
            this.ui = new SystemBackendUI(this.uiModel, config, lexiconManager, http);

            this.soundQueue = new SystemSoundQueue(lexiconManager);
            this.voiceExceptions = this.uiModel.SubscribeToVoiceExceptions(this.soundQueue.SelectVoiceFailed);
        }

        public override void Say(SayRequest request)
        {
            this.soundQueue.EnqueueSound(request.Voice, request.Source, request.Text);
            //SpeechNaudioBridge.NarratorTTS(request.Text);
        }




    
            
        

        public override void CancelAllSpeech()
        {
            this.soundQueue.CancelAllSounds();
        }

        public override void CancelSay(TextSource source)
        {
            this.soundQueue.CancelFromSource(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            this.ui.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            return this.soundQueue.GetCurrentlySpokenTextSource();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.voiceExceptions.Dispose();
                this.soundQueue.Dispose();
            }
        }
    }
}