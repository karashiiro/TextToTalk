﻿using System;
using System.Linq;
using ImGuiNET;
using TextToTalk.UI;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsBackendUI
{
    private readonly ElevenLabsBackendUIModel model;
    private readonly PluginConfiguration config;

    public ElevenLabsBackendUI(ElevenLabsBackendUIModel model, PluginConfiguration config)
    {
        this.model = model;
        this.config = config;
    }

    public void DrawSettings()
    {
        var apiKey = this.model.GetApiKey();
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API key", ref apiKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            this.model.LoginWith(apiKey);
        }

        var loginError = this.model.ElevenLabsLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(BackendUI.Red, $"Failed to login: {loginError}");
        }

        ImGui.SameLine();
        if (ImGui.Button($"Register##{MemoizedId.Create()}"))
        {
            WebBrowser.Open("https://beta.elevenlabs.io/");
        }

        ImGui.TextColored(BackendUI.HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var currentVoicePreset = this.model.GetCurrentVoicePreset();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.ElevenLabs).ToList();
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

        BackendUI.NewPresetButton<ElevenLabsVoicePreset>($"New preset##{MemoizedId.Create()}", this.config);

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

        {
            var voiceCategories = this.model.Voices;
            var voiceCategoriesFlat = voiceCategories.SelectMany(vc => vc.Value).ToList();
            var voiceDisplayNames = voiceCategoriesFlat.Select(v => v.Name).ToArray();
            var voiceIds = voiceCategoriesFlat.Select(v => v.VoiceId).ToArray();
            var voiceIndex = Array.IndexOf(voiceIds, currentVoicePreset.VoiceId);
            if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", voiceDisplayNames[voiceIndex]))
            {
                foreach (var (category, voices) in voiceCategories)
                {
                    ImGui.Selectable(category, false, ImGuiSelectableFlags.Disabled);
                    foreach (var voice in voices)
                    {
                        if (ImGui.Selectable($"  {voice.Name}"))
                        {
                            currentVoicePreset.VoiceId = voice.VoiceId;
                            this.config.Save();
                        }

                        if (voice.VoiceId == currentVoicePreset.VoiceId)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                }

                ImGui.EndCombo();
            }

            if (voiceCategoriesFlat.Count == 0)
            {
                ImGui.TextColored(BackendUI.Red,
                    "No voices were found. This might indicate a temporary service outage.");
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

        {
            ConfigComponents.ToggleUseGenderedVoicePresets(
                $"Use gendered voices##{MemoizedId.Create()}",
                this.config);

            ImGui.Spacing();
            if (this.config.UseGenderedVoicePresets)
            {
                BackendUI.GenderedPresetConfig("Polly", TTSBackend.ElevenLabs, this.config, presets);
            }
        }
    }
}