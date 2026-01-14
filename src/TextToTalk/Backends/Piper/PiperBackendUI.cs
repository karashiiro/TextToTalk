using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using System;
using System.IO;
using System.Linq;
using TextToTalk.UI;

namespace TextToTalk.Backends.Piper;

public class PiperBackendUI(PluginConfiguration config, PiperBackend piperBackend)
{
    private string[] availableModelPaths;
    private string[] modelDisplayNames;
    private int selectedModelIndex = -1;

    public void DrawVoicePresetOptions()
    {
        ImGui.TextColored(ImColor.HintColor, "Piper is a local neural TTS engine. Ensure you have downloaded models.");

        ImGui.Spacing();

        var currentVoicePreset = config.GetCurrentVoicePreset<PiperVoicePreset>();
        var presets = config.GetVoicePresetsForBackend(TTSBackend.Piper).ToList();

        if (presets.Count > 0 && currentVoicePreset != null)
        {
            var presetIndex = currentVoicePreset is not null ? presets.IndexOf(currentVoicePreset) : -1;
            if (ImGui.Combo($"Voice preset##{MemoizedId.Create()}", ref presetIndex,
                    presets.Select(p => p.Name).ToArray(), presets.Count))
            {
                config.SetCurrentVoicePreset(presets[presetIndex].Id);
                config.Save();
                selectedModelIndex = -1;
            }
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Create one to begin.");
        }
        else if (currentVoicePreset == null && presets.Count > 0)
        {
            config.SetCurrentVoicePreset(presets.First().Id);
        }

        BackendUI.NewPresetButton<PiperVoicePreset>($"New preset##{MemoizedId.Create()}", config);

        if (presets.Count == 0 || currentVoicePreset is null) return;

        ImGui.SameLine();
        BackendUI.DeletePresetButton($"Delete preset##{MemoizedId.Create()}", currentVoicePreset, TTSBackend.Piper, config);

        ImGui.Separator();

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            config.Save();
        }

        // --- Model Selection ---
        var piperDir = Path.Combine(config.GetPluginConfigDirectory(), "piper");
        var voicesDir = Path.Combine(piperDir, "voices");

        if (!Directory.Exists(voicesDir))
            Directory.CreateDirectory(voicesDir);

        availableModelPaths = Directory.GetFiles(voicesDir, "*.onnx", SearchOption.AllDirectories);
        modelDisplayNames = availableModelPaths.Select(Path.GetFileName).ToArray();

        if (availableModelPaths.Length > 0)
        {
            if (selectedModelIndex == -1 ||
                selectedModelIndex >= availableModelPaths.Length ||
                availableModelPaths[selectedModelIndex] != currentVoicePreset.ModelPath)
            {
                selectedModelIndex = Array.IndexOf(availableModelPaths, currentVoicePreset.ModelPath ?? "");
                if (selectedModelIndex == -1) selectedModelIndex = 0;
            }

            if (ImGui.Combo($"Voice Model (.onnx)##{MemoizedId.Create()}", ref selectedModelIndex, modelDisplayNames, modelDisplayNames.Length))
            {
                currentVoicePreset.ModelPath = availableModelPaths[selectedModelIndex];
                currentVoicePreset.InternalName = modelDisplayNames[selectedModelIndex].Replace(".onnx", "");
                config.Save();
            }
        }
        else
        {
            ImGui.TextColored(ImColor.Red, $"No .onnx models found in subdirectories of: {voicesDir}");
        }

        if (ImGui.Button($"Download Models##{MemoizedId.Create()}"))
        {
            _ = piperBackend.EnsurePiperAssetsDownloaded(config);
        }
        Components.Tooltip("Will download all English voices at medium quality (appx 1.5GB)");

        // --- Voice Parameters ---
        var speed = currentVoicePreset.Speed ?? 1f;
        if (ImGui.SliderFloat($"Speed##{MemoizedId.Create()}", ref speed, 0.5f, 3.0f, "%.2fx"))
        {
            currentVoicePreset.Speed = speed;
            config.Save();
        }

        var volume = (int)((currentVoicePreset.Volume ?? 1.0f) * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = MathF.Round((float)volume / 100, 2);
            config.Save();
        }

        if (ImGui.Button($"Test##{MemoizedId.Create()}"))
        {
            if (!string.IsNullOrEmpty(currentVoicePreset.ModelPath) && File.Exists(currentVoicePreset.ModelPath))
            {
                piperBackend.CancelSay(TextSource.Chat);
                piperBackend.Say($"Hello from Piper neural engine. This is a test message", currentVoicePreset,
                    TextSource.Chat);
            }
        }

        ImGui.Separator();

        ConfigComponents.ToggleUseGenderedVoicePresets($"Use gendered voices##{MemoizedId.Create()}", config);
        if (config.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("Piper", TTSBackend.Piper, config, presets);
        }
    }
}