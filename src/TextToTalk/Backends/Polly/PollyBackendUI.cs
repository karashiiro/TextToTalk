using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.RegularExpressions;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI.Dalamud.Lexicons;

namespace TextToTalk.Backends.Polly;

public class PollyBackendUI
{
    private static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly string[] Regions = RegionEndpoint.EnumerableAllRegions.Select(r => r.SystemName).ToArray();
    private static readonly string[] Engines = { Engine.Neural, Engine.Standard };

    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly LexiconManager lexiconManager;

    private readonly Func<PollyClient> getPolly;
    private readonly Action<PollyClient> setPolly;
    private readonly Func<IList<Voice>> getVoices;
    private readonly Action<IList<Voice>> setVoices;

    private string accessKey = string.Empty;
    private string secretKey = string.Empty;

    public PollyBackendUI(PluginConfiguration config, LexiconManager lexiconManager, HttpClient http,
        Func<PollyClient> getPolly, Action<PollyClient> setPolly, Func<IList<Voice>> getVoices, Action<IList<Voice>> setVoices)
    {
        this.getPolly = getPolly;
        this.setPolly = setPolly;
        this.getVoices = getVoices;
        this.setVoices = setVoices;

        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");
        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.config = config;
        this.lexiconComponent = new LexiconComponent(lexiconManager, lexiconRepository, config, () => config.PollyLexiconFiles);
        this.lexiconManager = lexiconManager;

        var credentials = PollyCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.accessKey = credentials.UserName;
            this.secretKey = credentials.Password;

            var regionEndpoint = RegionEndpoint.EnumerableAllRegions.FirstOrDefault(r => r.SystemName == this.config.PollyRegion)
                ?? RegionEndpoint.EUWest1;
            PollyLogin(regionEndpoint);
        }
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    public void DrawSettings(IConfigUIDelegates helpers)
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
            var username = Whitespace.Replace(this.accessKey, "");
            var password = Whitespace.Replace(this.secretKey, "");
            PollyCredentialManager.SaveCredentials(username, password);

            var regionEndpoint = RegionEndpoint.EnumerableAllRegions.FirstOrDefault(r => r.SystemName == this.config.PollyRegion);
            if (regionEndpoint == null)
            {
                ImGui.TextColored(Red, "Invalid region!");
            }
            else
            {
                PollyLogin(regionEndpoint);
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Register##TTTRegisterPollyAuth"))
        {
            WebBrowser.Open("https://docs.aws.amazon.com/polly/latest/dg/getting-started.html");
        }

        ImGui.TextColored(HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var engine = this.config.PollyEngine;
        var engineIndex = Array.IndexOf(Engines, engine);
        if (ImGui.Combo("Engine##TTTPollyEngine", ref engineIndex, Engines, Engines.Length))
        {
            this.config.PollyEngine = Engines[engineIndex];
            this.config.Save();

            var polly = this.getPolly.Invoke();
            var voices = polly?.GetVoicesForEngine(this.config.PollyEngine) ?? new List<Voice>();
            this.setVoices.Invoke(voices);
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

        this.lexiconComponent.Draw();
        ImGui.Spacing();

        {
            var voices = this.getVoices.Invoke();
            var voiceArray = voices.Select(v => v.Name).ToArray();
            var voiceIdArray = voices.Select(v => v.Id).ToArray();

            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox("Use gendered voices##TTTVoice2", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            ImGui.Spacing();
            if (useGenderedVoicePresets)
            {
                if (voices.Count == 0)
                {
                    ImGui.TextColored(Red, "No voices are available on this voice engine for the current region.\n" +
                                           "Please log in using a different region.");
                }

                var currentUngenderedVoiceId = this.config.PollyVoiceUngendered;
                var currentMaleVoiceId = this.config.PollyVoiceMale;
                var currentFemaleVoiceId = this.config.PollyVoiceFemale;

                var ungenderedVoiceIndex = Array.IndexOf(voiceIdArray, currentUngenderedVoiceId);
                if (ImGui.Combo("Ungendered voice##TTTVoice5", ref ungenderedVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.PollyVoiceUngendered = voiceIdArray[ungenderedVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Id == this.config.PollyVoiceUngendered))
                {
                    ImGuiVoiceNotSupported();
                }

                var maleVoiceIndex = Array.IndexOf(voiceIdArray, currentMaleVoiceId);
                if (ImGui.Combo("Male voice##TTTVoice3", ref maleVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.PollyVoiceMale = voiceIdArray[maleVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Id == this.config.PollyVoiceMale))
                {
                    ImGuiVoiceNotSupported();
                }

                var femaleVoiceIndex = Array.IndexOf(voiceIdArray, currentFemaleVoiceId);
                if (ImGui.Combo("Female voice##TTTVoice4", ref femaleVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.PollyVoiceFemale = voiceIdArray[femaleVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Id == this.config.PollyVoiceFemale))
                {
                    ImGuiVoiceNotSupported();
                }
            }
            else
            {
                var currentVoiceId = this.config.PollyVoice;
                var voiceIndex = Array.IndexOf(voiceIdArray, currentVoiceId);
                if (ImGui.Combo("Voice##TTTVoice1", ref voiceIndex, voiceArray, voices.Count))
                {
                    this.config.PollyVoice = voiceIdArray[voiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Id == this.config.PollyVoice))
                {
                    ImGuiVoiceNotSupported();
                }
            }
        }
    }

    private void PollyLogin(RegionEndpoint regionEndpoint)
    {
        var polly = this.getPolly.Invoke();
        polly?.Dispose();
        try
        {
            PluginLog.Log($"Logging into AWS region {regionEndpoint}.");
            polly = new PollyClient(this.accessKey, this.secretKey, regionEndpoint, this.lexiconManager);
            var voices = polly.GetVoicesForEngine(this.config.PollyEngine);
            this.setPolly.Invoke(polly);
            this.setVoices.Invoke(voices);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Failed to initialize AWS client.");
            PollyCredentialManager.DeleteCredentials();
        }
    }

    private static void ImGuiVoiceNotSupported()
    {
        ImGui.TextColored(Red, "Voice not supported on this engine");
    }
}