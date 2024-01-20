using System;
using System.Linq;
using ImGuiNET;
using TextToTalk.UI;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackendUI
{
    public void DrawLoginOptions(OpenAiApiConfig apiConfig)
    {
        var apiKey = apiConfig.ApiKey;
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API key", ref apiKey, 100,
            ImGuiInputTextFlags.Password);
        
        if (ImGui.Button($"Login##{MemoizedId.Create()}"))
        {
            OpenAiCredentialManager.SaveCredentials(apiKey);
            apiConfig.ApiKey = apiKey;
        }
    }

    public void DrawVoicePresetOptions(PluginConfiguration pluginConfiguration)
    {
        var currentVoicePreset = pluginConfiguration.GetCurrentVoicePreset<OpenAiVoicePreset>();
        var presets = pluginConfiguration.GetVoicePresetsForBackend(TTSBackend.OpenAi).ToList();

        if (presets.Count > 0 && currentVoicePreset != null)
        {
            var currentPresetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo($"Voice preset##{MemoizedId.Create()}", ref currentPresetIndex,
                presets.Select(p => p.Name).ToArray(), presets.Count))
            {
                pluginConfiguration.SetCurrentVoicePreset(presets[currentPresetIndex].Id);
            }
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(BackendUI.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }
        
        BackendUI.NewPresetButton<OpenAiVoicePreset>($"New preset##{MemoizedId.Create()}", pluginConfiguration);
        
        if (presets.Count == 0 || currentVoicePreset is null)
        {
            return;
        }
        
        ImGui.SameLine();
        BackendUI.DeletePresetButton($"Delete preset##{MemoizedId.Create()}", 
            currentVoicePreset, 
            TTSBackend.OpenAi,
            pluginConfiguration);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name ##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            pluginConfiguration.Save();
        }

        var voiceNames = OpenAiClient.Voices;
        if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", currentVoicePreset.VoiceName))
        {
            foreach (var voiceName in voiceNames)
            {
                if (ImGui.Selectable(voiceName, voiceName == currentVoicePreset.VoiceName))
                {
                    currentVoicePreset.VoiceName = voiceName;
                    pluginConfiguration.Save();
                }
            }
            ImGui.EndCombo();
        }
        
        var modelNames = OpenAiClient.Models;
        if (ImGui.BeginCombo($"Model##{MemoizedId.Create()}", currentVoicePreset.Model))
        {
            currentVoicePreset.Model ??= modelNames.First();
            foreach (var modelName in modelNames)
            {
                if (ImGui.Selectable(modelName, modelName == currentVoicePreset.Model))
                {
                    currentVoicePreset.Model = modelName;
                    pluginConfiguration.Save();
                }
            }
            ImGui.EndCombo();
        }

        var playbackRate = currentVoicePreset.PlaybackRate ?? 1;
        if (ImGui.SliderFloat($"Playback rate##{MemoizedId.Create()}", ref playbackRate, 0.25f, 4f, "%.2fx"))
        {
            currentVoicePreset.PlaybackRate = playbackRate;
            pluginConfiguration.Save();
        }

        var volume = (int)(currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float)Math.Round(volume / 100f, 2);
            pluginConfiguration.Save();
        }

        ConfigComponents.ToggleUseGenderedVoicePresets(
            $"Use gendered voice presets##{MemoizedId.Create()}",
            pluginConfiguration);
        ImGui.Spacing();
        if (pluginConfiguration.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("Polly", TTSBackend.OpenAi, pluginConfiguration, presets);
        }
    }
}