using System;
using System.Linq;
using ImGuiNET;
using TextToTalk.UI;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackendUI
{
    private readonly PluginConfiguration config;
    private readonly OpenAiBackendUIModel model;

    public OpenAiBackendUI(OpenAiBackendUIModel model, PluginConfiguration config)
    {
        this.model = model;
        this.config = config;
    }

    public void DrawLoginOptions()
    {
        var apiKey = model.ApiKey;
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API key", ref apiKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Login##{MemoizedId.Create()}"))
        {
            OpenAiCredentialManager.SaveCredentials(apiKey);
            model.ApiKey = apiKey;
        }
    }

    public void DrawVoicePresetOptions()
    {
        var currentVoicePreset = config.GetCurrentVoicePreset<OpenAiVoicePreset>();
        var presets = config.GetVoicePresetsForBackend(TTSBackend.OpenAi).ToList();

        if (presets.Count > 0 && currentVoicePreset != null)
        {
            var currentPresetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo($"Voice preset##{MemoizedId.Create()}", ref currentPresetIndex,
                    presets.Select(p => p.Name).ToArray(), presets.Count))
                config.SetCurrentVoicePreset(presets[currentPresetIndex].Id);
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(BackendUI.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<OpenAiVoicePreset>($"New preset##{MemoizedId.Create()}", config);

        if (presets.Count == 0 || currentVoicePreset is null) return;

        ImGui.SameLine();
        BackendUI.DeletePresetButton($"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.OpenAi,
            config);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name ##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            config.Save();
        }

        var voiceNames = OpenAiClient.Voices;
        if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", currentVoicePreset.VoiceName))
        {
            foreach (var voiceName in voiceNames)
                if (ImGui.Selectable(voiceName, voiceName == currentVoicePreset.VoiceName))
                {
                    currentVoicePreset.VoiceName = voiceName;
                    config.Save();
                }

            ImGui.EndCombo();
        }

        var modelNames = OpenAiClient.Models;
        if (ImGui.BeginCombo($"Model##{MemoizedId.Create()}", currentVoicePreset.Model))
        {
            currentVoicePreset.Model ??= modelNames.First();
            foreach (var modelName in modelNames)
                if (ImGui.Selectable(modelName, modelName == currentVoicePreset.Model))
                {
                    currentVoicePreset.Model = modelName;
                    config.Save();
                }

            ImGui.EndCombo();
        }

        var playbackRate = currentVoicePreset.PlaybackRate ?? 1;
        if (ImGui.SliderFloat($"Playback rate##{MemoizedId.Create()}", ref playbackRate, 0.25f, 4f, "%.2fx"))
        {
            currentVoicePreset.PlaybackRate = playbackRate;
            config.Save();
        }

        var volume = (int) (currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float) Math.Round(volume / 100f, 2);
            config.Save();
        }

        ConfigComponents.ToggleUseGenderedVoicePresets(
            $"Use gendered voice presets##{MemoizedId.Create()}",
            config);
        ImGui.Spacing();
        if (config.UseGenderedVoicePresets) BackendUI.GenderedPresetConfig("Polly", TTSBackend.OpenAi, config, presets);
    }
}