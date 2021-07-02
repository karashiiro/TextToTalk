using AdysTech.CredentialManager;
using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using Gender = TextToTalk.GameEnums.Gender;

namespace TextToTalk.Backends.Polly
{
    public class AmazonPollyBackend : VoiceBackend
    {
        private const string CredentialsTarget = "TextToTalk_AccessKeys_AmazonPolly";

        private static readonly Vector4 HintColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

        private readonly PluginConfiguration config;
        private readonly IList<Voice> voices;

        private PollyClient polly;

        private string accessKey = string.Empty;
        private string secretKey = string.Empty;

        public AmazonPollyBackend(PluginConfiguration config)
        {
            var credentials = CredentialManager.GetCredentials(CredentialsTarget);

            if (credentials != null)
            {
                this.accessKey = credentials.UserName;
                this.secretKey = credentials.Password;
                this.polly = new PollyClient(credentials.UserName, credentials.Password, RegionEndpoint.EUWest1);
            }

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
            ImGui.InputTextWithHint("##TTTAccessKey", "Access key", ref this.accessKey, 100, ImGuiInputTextFlags.Password);
            ImGui.InputTextWithHint("##TTTSecretKey", "Secret key", ref this.secretKey, 100, ImGuiInputTextFlags.Password);

            if (ImGui.Button("Save##TTTSavePollyAuth"))
            {
                var credentials = new NetworkCredential(this.accessKey, this.secretKey);
                CredentialManager.SaveCredentials(CredentialsTarget, credentials);

                this.polly?.Dispose();
                this.polly = new PollyClient(this.accessKey, this.secretKey, RegionEndpoint.EUWest1);
            }

            ImGui.TextColored(HintColor, "Credentials secured with Windows Credential Manager");

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
                this.polly?.Dispose();
            }
        }
    }
}