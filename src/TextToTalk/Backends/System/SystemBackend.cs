﻿using System;
using System.Net.Http;

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
            this.ui = new SystemBackendUI(this.uiModel, config, lexiconManager, http);

            this.soundQueue = new SystemSoundQueue(lexiconManager);
            this.voiceExceptions = this.uiModel.SubscribeToVoiceExceptions(this.soundQueue.SelectVoiceFailed);
        }

        public override void Say(TextSource source, VoicePreset voice, string speaker, string text)
        {
            this.soundQueue.EnqueueSound(voice, source, text);
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