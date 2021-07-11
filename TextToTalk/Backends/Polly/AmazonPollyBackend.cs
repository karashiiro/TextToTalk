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
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Gender = TextToTalk.GameEnums.Gender;

namespace TextToTalk.Backends.Polly
{
    public class AmazonPollyBackend : VoiceBackend
    {
        private const string CredentialsTarget = "TextToTalk_AccessKeys_AmazonPolly";

        private static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
        private static readonly Vector4 Red = new(1, 0, 0, 1);

        private static readonly string[] Regions = RegionEndpoint.EnumerableAllRegions.Select(r => r.SystemName).ToArray();
        private static readonly string[] Engines = { Engine.Neural, Engine.Standard };

        private readonly PluginConfiguration config;

        private PollyClient polly;
        private IList<Voice> voices;

        private string accessKey = string.Empty;
        private string secretKey = string.Empty;

        public AmazonPollyBackend(PluginConfiguration config)
        {
            this.config = config;

            TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFF0099FF);

            var credentials = CredentialManager.GetCredentials(CredentialsTarget);

            if (credentials != null)
            {
                this.accessKey = credentials.UserName;
                this.secretKey = credentials.Password;
                this.polly = new PollyClient(credentials.UserName, credentials.Password, RegionEndpoint.EUWest1);
                this.voices = this.polly.GetVoicesForEngine(this.config.PollyEngine);
            }
            else
            {
                this.voices = new List<Voice>();
            }
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
            
            text = $"<speak><prosody rate=\"{this.config.PollyPlaybackRate}%\">{text}</prosody></speak>";

            _ = this.polly.Say(this.config.PollyEngine, voiceId, this.config.PollySampleRate, this.config.PollyVolume, text);
        }

        public override void CancelSay()
        {
            _ = this.polly.Cancel();
        }


        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
        public override void DrawSettings(ImExposedFunctions helpers)
        {
            var region = this.config.PollyRegion;
            var regionIndex = Array.IndexOf(Regions, region);
            if (ImGui.Combo("Region##TTTPollyRegion", ref regionIndex, Regions, Regions.Length))
            {
                this.config.PollyRegion = Regions[regionIndex];
                this.config.Save();
            }

            ImGui.InputTextWithHint("##TTTPollyAccessKey", "Access key", ref this.accessKey, 100, ImGuiInputTextFlags.Password);
            ImGui.InputTextWithHint("##TTTPollySecretKey", "Secret key", ref this.secretKey, 100, ImGuiInputTextFlags.Password);

            if (ImGui.Button("Save and Login##TTTSavePollyAuth"))
            {
                var credentials = new NetworkCredential(Whitespace.Replace(this.accessKey, ""), Whitespace.Replace(this.secretKey, ""));
                CredentialManager.SaveCredentials(CredentialsTarget, credentials);

                var regionEndpoint = RegionEndpoint.EnumerableAllRegions.FirstOrDefault(r => r.SystemName == this.config.PollyRegion);
                if (regionEndpoint == null)
                {
                    ImGui.TextColored(Red, "Region invalid!");
                }
                else
                {
                    this.polly?.Dispose();
                    this.polly = new PollyClient(this.accessKey, this.secretKey, regionEndpoint);
                }
            }

            ImGui.TextColored(HintColor, "Credentials secured with Windows Credential Manager");

            ImGui.Spacing();

            var engine = this.config.PollyEngine;
            var engineIndex = Array.IndexOf(Engines, engine);
            if (ImGui.Combo("Engine##TTTPollyEngine", ref engineIndex, Engines, Engines.Length))
            {
                this.config.PollyEngine = Engines[engineIndex];
                this.config.Save();

                this.voices = this.polly.GetVoicesForEngine(this.config.PollyEngine);
            }

            var validSampleRates = new[] { "8000", "16000", "22050", "24000" };
            var sampleRate = this.config.PollySampleRate.ToString();
            var sampleRateIndex = Array.IndexOf(validSampleRates, sampleRate);
            if (ImGui.Combo("Sample rate##TTTVoice6", ref sampleRateIndex, validSampleRates, validSampleRates.Length))
            {
                this.config.PollySampleRate = int.Parse(validSampleRates[sampleRateIndex]);
                this.config.Save();
            }

            var playbackRate = this.config.PollyPlaybackRate;
            if (ImGui.SliderInt("Playback rate##TTTVoice8", ref playbackRate, 20, 200, "%d%%",
                ImGuiSliderFlags.AlwaysClamp))
            {
                this.config.PollyPlaybackRate = playbackRate;
                this.config.Save();
            }

            var volume = (int)(this.config.PollyVolume * 100);
            if (ImGui.SliderInt("Volume##TTTVoice7", ref volume, 0, 100))
            {
                this.config.PollyVolume = (float)Math.Round((double)volume / 100, 2);
                this.config.Save();
            }

            var voiceArray = this.voices.Select(v => v.Name).ToArray();
            var voiceIdArray = this.voices.Select(v => v.Id).ToArray();

            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox("Use gendered voices##TTTVoice2", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            if (useGenderedVoicePresets)
            {
                var currentUngenderedVoiceId = this.config.PollyVoiceUngendered;
                var currentMaleVoiceId = this.config.PollyVoiceMale;
                var currentFemaleVoiceId = this.config.PollyVoiceFemale;

                var ungenderedVoiceIndex = Array.IndexOf(voiceIdArray, currentUngenderedVoiceId);
                if (ImGui.Combo("Ungendered voice##TTTVoice5", ref ungenderedVoiceIndex, voiceArray, this.voices.Count))
                {
                    this.config.PollyVoiceUngendered = voiceIdArray[ungenderedVoiceIndex];
                    this.config.Save();
                }

                if (this.voices.Count > 0 && !this.voices.Any(v => v.Id == this.config.PollyVoiceUngendered))
                {
                    ImGuiVoiceNotSupported();
                }

                var maleVoiceIndex = Array.IndexOf(voiceIdArray, currentMaleVoiceId);
                if (ImGui.Combo("Male voice##TTTVoice3", ref maleVoiceIndex, voiceArray, this.voices.Count))
                {
                    this.config.PollyVoiceMale = voiceIdArray[maleVoiceIndex];
                    this.config.Save();
                }

                if (this.voices.Count > 0 && !this.voices.Any(v => v.Id == this.config.PollyVoiceMale))
                {
                    ImGuiVoiceNotSupported();
                }

                var femaleVoiceIndex = Array.IndexOf(voiceIdArray, currentFemaleVoiceId);
                if (ImGui.Combo("Female voice##TTTVoice4", ref femaleVoiceIndex, voiceArray, this.voices.Count))
                {
                    this.config.PollyVoiceFemale = voiceIdArray[femaleVoiceIndex];
                    this.config.Save();
                }

                if (this.voices.Count > 0 && !this.voices.Any(v => v.Id == this.config.PollyVoiceFemale))
                {
                    ImGuiVoiceNotSupported();
                }
            }
            else
            {
                var currentVoiceId = this.config.PollyVoice;
                var voiceIndex = Array.IndexOf(voiceIdArray, currentVoiceId);
                if (ImGui.Combo("Voice##TTTVoice1", ref voiceIndex, voiceArray, this.voices.Count))
                {
                    this.config.PollyVoice = voiceIdArray[voiceIndex];
                    this.config.Save();
                }

                if (this.voices.Count > 0 && !this.voices.Any(v => v.Id == this.config.PollyVoice))
                {
                    ImGuiVoiceNotSupported();
                }
            }
        }

        private static void ImGuiVoiceNotSupported()
        {
            ImGui.TextColored(Red, "Voice not supported on this engine");
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