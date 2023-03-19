using Amazon.Polly;
using ImGuiNET;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI;
using TextToTalk.UI.Lexicons;

namespace TextToTalk.Backends.Polly;

public class PollyBackendUI
{
    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly PollyBackendUIModel model;

    public PollyBackendUI(PollyBackendUIModel model, PluginConfiguration config, LexiconManager lexiconManager, HttpClient http)
    {
        this.model = model;

        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");
        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.config = config;
        this.lexiconComponent =
            new LexiconComponent(lexiconManager, lexiconRepository, config, () => config.PollyLexiconFiles);
    }

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        var region = this.model.GetCurrentRegion();
        var regionIndex = Array.IndexOf(this.model.Regions, region.SystemName);
        if (ImGui.Combo($"Region##{MemoizedId.Create()}", ref regionIndex, this.model.Regions, this.model.Regions.Length))
        {
            this.model.SetCurrentRegion(this.model.Regions[regionIndex]);
        }

        var (accessKey, secretKey) = this.model.GetKeyPair();
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "Access key", ref accessKey, 100,
            ImGuiInputTextFlags.Password);
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "Secret key", ref secretKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            this.model.LoginWith(accessKey, secretKey);
        }

        var loginError = this.model.PollyLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(BackendUI.Red, $"Failed to login: {loginError}");
        }

        ImGui.SameLine();
        if (ImGui.Button($"Register##{MemoizedId.Create()}"))
        {
            WebBrowser.Open("https://docs.aws.amazon.com/polly/latest/dg/getting-started.html");
        }

        ImGui.TextColored(BackendUI.HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var currentVoicePreset = this.model.GetCurrentVoicePreset();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.AmazonPolly).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any() && currentVoicePreset != null)
        {
            var presetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo($"Preset##{MemoizedId.Create()}", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                    presets.Count))
            {
                this.model.SetCurrentVoicePreset(presets[presetIndex].Id);
            }
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(BackendUI.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<PollyVoicePreset>($"New preset##{MemoizedId.Create()}", this.config);

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        BackendUI.DeletePresetButton(
            $"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.AmazonPolly,
            this.config);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        var engine = this.model.GetCurrentEngine();
        var engineIndex = Array.IndexOf(this.model.Engines, engine.Value);
        if (ImGui.Combo($"Engine##{MemoizedId.Create()}", ref engineIndex, this.model.Engines, this.model.Engines.Length))
        {
            this.model.SetCurrentEngine(this.model.Engines[engineIndex]);
        }

        {
            var voices = this.model.CurrentEngineVoices;
            var voiceArray = voices.Select(v => v.Name).ToArray();
            var voiceIdArray = voices.Select(v => v.Id).ToArray();
            var voiceIndex = Array.IndexOf(voiceIdArray, currentVoicePreset.VoiceName);
            if (ImGui.Combo($"Voice##{MemoizedId.Create()}", ref voiceIndex, voiceArray, voices.Count))
            {
                currentVoicePreset.VoiceName = voiceIdArray[voiceIndex];
                this.config.Save();
            }

            switch (voices.Count)
            {
                case 0:
                    ImGui.TextColored(BackendUI.Red,
                        "No voices are available on this voice engine for the current region.\n" +
                        "Please log in using a different region.");
                    break;
                case > 0 when !voices.Any(v => v.Id == currentVoicePreset.VoiceName):
                    BackendUI.ImGuiVoiceNotSupported();
                    break;
            }
        }

        var validSampleRates = new[] { "8000", "16000", "22050", "24000" };
        var sampleRate = currentVoicePreset.SampleRate.ToString();
        var sampleRateIndex = Array.IndexOf(validSampleRates, sampleRate);
        if (ImGui.Combo($"Sample rate##{MemoizedId.Create()}", ref sampleRateIndex, validSampleRates,
                validSampleRates.Length))
        {
            currentVoicePreset.SampleRate = int.Parse(validSampleRates[sampleRateIndex]);
            this.config.Save();
        }

        var playbackRate = currentVoicePreset.PlaybackRate;
        if (ImGui.SliderInt($"Playback rate##{MemoizedId.Create()}", ref playbackRate, 20, 200, "%d%%",
                ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.PlaybackRate = playbackRate;
            this.config.Save();
        }

        var volume = (int)(currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float)Math.Round((double)volume / 100, 2);
            this.config.Save();
        }

        if (engine == Engine.Neural && (currentVoicePreset.VoiceName == VoiceId.Matthew ||
                                        currentVoicePreset.VoiceName == VoiceId.Joanna ||
                                        currentVoicePreset.VoiceName == VoiceId.Lupe ||
                                        currentVoicePreset.VoiceName == VoiceId.Amy))
        {
            var domain = currentVoicePreset.AmazonDomainName;
            var newscaster = domain == "news";
            if (ImGui.Checkbox($"Newscaster style (select voices only)##{MemoizedId.Create()}", ref newscaster))
            {
                currentVoicePreset.AmazonDomainName = newscaster ? "news" : "";
                this.config.Save();
            }
        }

        this.lexiconComponent.Draw();
        ImGui.Spacing();

        {
            ConfigComponents.ToggleUseGenderedVoicePresets(
                    $"Use gendered voices##{MemoizedId.Create()}",
                    this.config);

            ImGui.Spacing();
            if (this.config.UseGenderedVoicePresets)
            {
                BackendUI.GenderedPresetConfig("Polly", TTSBackend.AmazonPolly, this.config, presets);
            }
        }
    }
}