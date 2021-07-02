using Amazon;
using Amazon.Polly.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Polly;
using ImGuiNET;
using Gender = TextToTalk.GameEnums.Gender;

namespace TextToTalk.Backends.Polly
{
    public class AmazonPollyBackend : VoiceBackend
    {
        private readonly PollyClient polly;
        private readonly PluginConfiguration config;

        private readonly IList<Voice> voices;

        public AmazonPollyBackend(PluginConfiguration config)
        {
            this.polly = new PollyClient("", "", RegionEndpoint.EUWest1);
            this.config = config;

            this.voices = this.polly.GetVoicesForEngine(this.config.PollyEngine);
        }

        public override void Say(Gender gender, string text)
        {
            var voiceIdStr = gender switch
            {
                Gender.Male => this.config.PollyVoiceMale,
                Gender.Female => this.config.PollyVoiceFemale,
                _ => this.config.PollyVoice,
            };

            var voiceId = this.voices
                .Select(v => v.Id)
                .FirstOrDefault(id => id == voiceIdStr) ?? VoiceId.Matthew;

            _ = this.polly.Say(voiceId, text);
        }

        public override void CancelSay()
        {
            this.polly.Cancel();
        }

        public override void DrawSettings(ImExposedFunctions helpers)
        {
            var voiceArray = voices.Select(v => v.Name).ToArray();
            var voiceIdArray = voices.Select(v => v.Id).ToArray();

            var currentVoiceId = this.config.PollyVoice;

            var voiceIndex = Array.IndexOf(voiceIdArray, currentVoiceId);
            if (ImGui.Combo("Male voice##TTTVoice3", ref voiceIndex, voiceArray, voices.Count))
            {
                this.config.PollyVoice = voiceIdArray[voiceIndex];
                this.config.Save();
            }

            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox("Use gendered voices##TTTVoice2", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            if (useGenderedVoicePresets)
            {
                var currentMaleVoiceId = this.config.PollyVoiceMale;
                var currentFemaleVoiceId = this.config.PollyVoiceFemale;

                var maleVoiceIndex = Array.IndexOf(voiceIdArray, currentMaleVoiceId);
                if (ImGui.Combo("Male voice##TTTVoice3", ref maleVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.PollyVoiceMale = voiceIdArray[maleVoiceIndex];
                    this.config.Save();
                }

                var femaleVoiceIndex = Array.IndexOf(voiceIdArray, currentFemaleVoiceId);
                if (ImGui.Combo("Female voice##TTTVoice4", ref femaleVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.PollyVoiceFemale = voiceIdArray[femaleVoiceIndex];
                    this.config.Save();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.polly.Dispose();
            }
        }
    }
}