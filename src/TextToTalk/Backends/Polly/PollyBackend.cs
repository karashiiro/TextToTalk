using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Gender = TextToTalk.GameEnums.Gender;

namespace TextToTalk.Backends.Polly
{
    public class PollyBackend : VoiceBackend
    {
        private readonly PluginConfiguration config;
        private readonly PollyBackendUI ui;

        private PollyClient polly;
        private IList<Voice> voices;

        public PollyBackend(PluginConfiguration config, HttpClient http)
        {
            TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFF0099FF);

            this.config = config;
            this.voices = new List<Voice>();

            var lexiconManager = new DalamudLexiconManager();
            LexiconUtils.LoadFromConfigPolly(lexiconManager, config);

            this.ui = new PollyBackendUI(config, lexiconManager, http,
                () => this.polly, p => this.polly = p, () => this.voices, v => this.voices = v);
        }

        public override void Say(TextSource source, Gender gender, string text)
        {
            var voiceIdStr = this.config.PollyVoice;
            if (this.config.UseGenderedVoicePresets)
            {
                voiceIdStr = gender switch
                {
                    Gender.Male => this.config.PollyVoiceMale,
                    Gender.Female => this.config.PollyVoiceFemale,
                    _ => this.config.PollyVoiceUngendered,
                };
            }

            // Find the configured voice in the voice list, and fall back to Matthew
            // if it wasn't found in order to avoid a plugin crash.
            var voiceId = this.voices
                .Select(v => v.Id)
                .FirstOrDefault(id => id == voiceIdStr) ?? VoiceId.Matthew;

            _ = this.polly.Say(this.config.PollyEngine, voiceId, this.config.PollySampleRate, this.config.PollyPlaybackRate, this.config.PollyVolume, source, text);
        }

        public override void CancelAllSpeech()
        {
            _ = this.polly.CancelAllSounds();
        }

        public override void CancelSay(TextSource source)
        {
            _ = this.polly.CancelFromSource(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            this.ui.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
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