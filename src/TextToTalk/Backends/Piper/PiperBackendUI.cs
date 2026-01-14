using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TextToTalk.UI;

namespace TextToTalk.Backends.Piper;

public class PiperBackendUI(PluginConfiguration config, PiperBackend piperBackend)
{
    private string[] availableModelPaths;
    private string[] modelDisplayNames;
    private int selectedModelIndex = -1;

    public class PiperModelInfo
    {
        public string FullPath { get; set; }
        public string DisplayName { get; set; }
        public string Quality { get; set; } // low, medium, high

        public static PiperModelInfo FromPath(string onnxPath)
        {
            var jsonPath = onnxPath + ".json";
            if (!File.Exists(jsonPath)) return null;

            try
            {
                var json = File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var lang = root.GetProperty("language").GetProperty("code").GetString();
                var dataset = root.GetProperty("dataset").GetString();
                var quality = root.GetProperty("audio").GetProperty("quality").GetString();

                return new PiperModelInfo
                {
                    FullPath = onnxPath,
                    DisplayName = $"{lang}: {dataset} ({quality})",
                    Quality = quality?.ToLower() ?? "medium"
                };
            }
            catch { return null; }
        }
    }

    private List<PiperModelInfo> sortedModels = new();
    private string[] sortedDisplayNames = Array.Empty<string>();
    
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

        // Model Selection
        var voicesDir = Path.Combine(config.GetPluginConfigDirectory(), "piper", "voices");
        if (!Directory.Exists(voicesDir)) Directory.CreateDirectory(voicesDir);

        var files = Directory.GetFiles(voicesDir, "*.onnx", SearchOption.AllDirectories);
        var allModels = files.Select(PiperModelInfo.FromPath).Where(m => m != null).ToList();

        if (allModels.Count > 0)
        {
            ImGui.Text("Voice Model Selection");

            var currentModel = allModels.FirstOrDefault(m => m.FullPath == currentVoicePreset.ModelPath);
            string previewValue = currentModel?.DisplayName ?? "Select a model...";

            if (ImGui.BeginCombo($"##ModelSelect{MemoizedId.Create()}", previewValue))
            {
                var qualities = new[] { "high", "medium", "low" };

                foreach (var quality in qualities)
                {
                    var modelsInSection = allModels.Where(m => m.Quality == quality).OrderBy(m => m.DisplayName).ToList();

                    if (modelsInSection.Count > 0)
                    {
                        string headerText = quality switch
                        {
                            "high" => "HIGH Quality - 24khz - higher latency",
                            "medium" => "MEDIUM Quality - 22.5khz - mid latency",
                            "low" => "LOW Quality - 16khz - lower latency",
                            _ => quality.ToUpper()
                        };

                        ImGui.Spacing();
                        ImGui.TextDisabled(headerText);
                        ImGui.Separator();

                        foreach (var model in modelsInSection)
                        {
                            bool isSelected = currentVoicePreset.ModelPath == model.FullPath;

                            if (ImGui.Selectable($"{model.DisplayName}##{model.FullPath}", isSelected))
                            {
                                currentVoicePreset.ModelPath = model.FullPath;
                                currentVoicePreset.InternalName = Path.GetFileNameWithoutExtension(model.FullPath);
                                config.Save();
                            }

                            if (isSelected) ImGui.SetItemDefaultFocus();
                        }
                    }
                }
                ImGui.EndCombo();
            }
        }
        else
        {
            ImGui.TextColored(ImColor.Red, "No voice models found.");
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