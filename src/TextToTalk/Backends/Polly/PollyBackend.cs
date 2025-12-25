using System;
using System.Net.Http;
using Dalamud.Bindings.ImGui;

namespace TextToTalk.Backends.Polly
{
    public class PollyBackend : VoiceBackend
    {
        private readonly PollyBackendUI ui;
        private readonly PollyBackendUIModel uiModel;

        public PollyBackend(PluginConfiguration config, HttpClient http)
        {
            TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFF0099FF);

            var lexiconManager = new DalamudLexiconManager();
            this.uiModel = new PollyBackendUIModel(config, lexiconManager);

            LexiconUtils.LoadFromConfigPolly(lexiconManager, config);

            this.ui = new PollyBackendUI(this.uiModel, config, lexiconManager, http, this);
        }

        public override void Say(SayRequest request)
        {
            if (request.Voice is not PollyVoicePreset pollyVoicePreset)
            {
                throw new InvalidOperationException("Invalid voice preset provided.");
            }

            if (this.uiModel.Polly == null)
            {
                DetailedLog.Warn("Polly client has not yet been initialized");
                return;
            }

            _ = this.uiModel.Polly.Say(pollyVoicePreset.VoiceEngine, pollyVoicePreset.VoiceName,
                pollyVoicePreset.AmazonDomainName, pollyVoicePreset.SampleRate, pollyVoicePreset.PlaybackRate,
                pollyVoicePreset.Volume, request.Source, request.Text);
        }

        public override void CancelAllSpeech()
        {
            if (this.uiModel.Polly == null)
            {
                DetailedLog.Warn("Polly client has not yet been initialized");
                return;
            }

            _ = this.uiModel.Polly.CancelAllSounds();
        }

        public override void CancelSay(TextSource source)
        {
            if (this.uiModel.Polly == null)
            {
                DetailedLog.Warn("Polly client has not yet been initialized");
                return;
            }

            _ = this.uiModel.Polly.CancelFromSource(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            this.ui.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            if (this.uiModel.Polly == null)
            {
                DetailedLog.Warn("Polly client has not yet been initialized");
                return TextSource.None;
            }

            return this.uiModel.Polly.GetCurrentlySpokenTextSource();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.uiModel.Dispose();
            }
        }
    }
}