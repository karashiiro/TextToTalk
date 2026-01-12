using System;
using System.Net.Http;
using System.Threading;

namespace TextToTalk.Backends.System
{
    public class SystemBackend : VoiceBackend
    {
        private readonly SystemBackendUIModel uiModel;
        private readonly SystemBackendUI ui;
        private readonly SystemSoundQueue soundQueue;
        private readonly IDisposable voiceExceptions;


        public SystemBackend(PluginConfiguration config, HttpClient http)
        {
            var lexiconManager = new DalamudLexiconManager();
            LexiconUtils.LoadFromConfigSystem(lexiconManager, config);

            this.uiModel = new SystemBackendUIModel();
            this.ui = new SystemBackendUI(this.uiModel, config, lexiconManager, http, this);

            this.soundQueue = new SystemSoundQueue(lexiconManager, config);
            this.voiceExceptions = this.uiModel.SubscribeToVoiceExceptions(this.soundQueue.SelectVoiceFailed);
        }

        public override void DrawStyles(IConfigUIDelegates helpers)
        {
            helpers.OpenVoiceStylesConfig();
        }
        public override void Say(SayRequest request)
        {
            this.soundQueue.EnqueueSound(request.Voice, request.Source, request.Text);
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