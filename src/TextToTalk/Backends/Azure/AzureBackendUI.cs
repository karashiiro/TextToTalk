﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using ImGuiNET;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI;
using TextToTalk.UI.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureBackendUI
{
    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly AzureBackendUIModel model;

    public AzureBackendUI(AzureBackendUIModel model, PluginConfiguration config, LexiconManager lexiconManager,
        HttpClient http)
    {
        this.model = model;

        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");
        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.config = config;
        this.lexiconComponent =
            new LexiconComponent(lexiconManager, lexiconRepository, config, Array.Empty<string>);
    }

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        var (region, subscriptionKey) = this.model.GetLoginInfo();
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "Region", ref region, 100);
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "Subscription key", ref subscriptionKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            this.model.LoginWith(region, subscriptionKey);
        }

        var loginError = this.model.AzureLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(BackendUI.Red, $"Failed to login: {loginError}");
        }

        ImGui.SameLine();
        if (ImGui.Button($"Register##{MemoizedId.Create()}"))
        {
            WebBrowser.Open(
                "https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/index-text-to-speech");
        }

        ImGui.TextColored(BackendUI.HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var currentVoicePreset = this.model.GetCurrentVoicePreset();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.Azure).ToList();
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
        else
        {
            ImGui.TextColored(BackendUI.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<AzureVoicePreset>($"New preset##{MemoizedId.Create()}", this.config);

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        BackendUI.DeletePresetButton(
            $"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.Azure,
            this.config);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        {
            var voices = this.model.Voices;
            string?[] voiceArray = voices.ToArray();
            var voiceIndex = Array.IndexOf(voiceArray, currentVoicePreset.VoiceName);
            if (ImGui.Combo($"Voice##{MemoizedId.Create()}", ref voiceIndex, voiceArray, voices.Count))
            {
                currentVoicePreset.VoiceName = voiceArray[voiceIndex];
                this.config.Save();
            }

            switch (voices.Count)
            {
                case 0:
                    ImGui.TextColored(BackendUI.Red,
                        "No voices are available on this voice engine for the current region.\n" +
                        "Please log in using a different region.");
                    break;
                case > 0 when !voices.Any(v => v == currentVoicePreset.VoiceName):
                    BackendUI.ImGuiVoiceNotSelected();
                    break;
            }
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

        this.lexiconComponent.Draw();
        ImGui.Spacing();

        {
            Components.Toggle(
                    $"Use gendered voices##{MemoizedId.Create()}",
                    this.config,
                    cfg => cfg.UseGenderedVoicePresets)
                .AndThen(this.config.Save);

            ImGui.Spacing();
            if (this.config.UseGenderedVoicePresets)
            {
                BackendUI.GenderedPresetConfig("Azure", TTSBackend.Azure, this.config, presets);
            }
        }
    }
}