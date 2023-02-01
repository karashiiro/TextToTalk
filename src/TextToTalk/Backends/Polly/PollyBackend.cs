using System;
using Amazon.Polly.Model;
using ImGuiNET;
using System.Collections.Generic;
using System.Net.Http;
using Dalamud.Logging;

namespace TextToTalk.Backends.Polly
{
    public class PollyBackend : VoiceBackend
    {
        private readonly PollyBackendUI ui;

        private PollyClient polly;

        public PollyBackend(PluginConfiguration config, HttpClient http)
        {
            TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFF0099FF);

            var lexiconManager = new DalamudLexiconManager();
            LexiconUtils.LoadFromConfigPolly(lexiconManager, config);

            IList<Voice> voices = new List<Voice>();
            this.ui = new PollyBackendUI(config, lexiconManager, http,
                () => this.polly, p => this.polly = p, () => voices, v => voices = v);
        }

        public override void Say(TextSource source, VoicePreset preset, string text)
        {
            if (preset is not PollyVoicePreset pollyVoicePreset)
            {
                throw new InvalidOperationException("Invalid voice preset provided.");
            }

            if (this.polly == null)
            {
                PluginLog.LogWarning("Polly client has not yet been initialized");
                return;
            }

            _ = this.polly.Say(pollyVoicePreset.VoiceEngine, pollyVoicePreset.VoiceName, pollyVoicePreset.SampleRate,
                pollyVoicePreset.PlaybackRate, pollyVoicePreset.Volume, source, text);
        }

        public override void CancelAllSpeech()
        {
            if (this.polly == null)
            {
                PluginLog.LogWarning("Polly client has not yet been initialized");
                return;
            }

            _ = this.polly.CancelAllSounds();
        }

        public override void CancelSay(TextSource source)
        {
            if (this.polly == null)
            {
                PluginLog.LogWarning("Polly client has not yet been initialized");
                return;
            }

            _ = this.polly.CancelFromSource(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            this.ui.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            if (this.polly == null)
            {
                PluginLog.LogWarning("Polly client has not yet been initialized");
                return TextSource.None;
            }

            return this.polly.GetCurrentlySpokenTextSource();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.polly?.Dispose();
            }
        }
    }
}