using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TextToTalk.UI;
using PiperSharp;
using PiperSharp.Models;

namespace TextToTalk.Backends.Piper;

public class PiperBackendUI(PluginConfiguration config, PiperBackend piperBackend)
{
    private string[] availableModelPaths;
    private string[] modelDisplayNames;
    private int selectedModelIndex = -1;

    private bool showDownloader = false;
    private IDictionary<string, VoiceModel> remoteModels;
    private string searchQuery = "";
    private List<PiperModelInfo> cachedModels = new(); // For live list updates
    private DateTime lastScan = DateTime.MinValue;
    private bool isScanning = false;

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
    private string voicesFolderSize = "0 MB";

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

        // 1. REFACTORED MODEL SCANNING (Async polling for new downloads)
        if (!isScanning && (DateTime.Now - lastScan).TotalSeconds > 3)
        {
            lastScan = DateTime.Now;
            isScanning = true;

            Task.Run(() =>
            {
                try
                {
                    var voicesDir = Path.Combine(config.GetPluginConfigDirectory(), "piper", "voices");
                    if (Directory.Exists(voicesDir))
                    {
                        // 1. Existing Model Scanning logic
                        var files = Directory.GetFiles(voicesDir, "*.onnx", SearchOption.AllDirectories);
                        cachedModels = files.Select(PiperModelInfo.FromPath).Where(m => m != null).ToList();

                        // 2. NEW: Calculate Folder Size
                        var dirInfo = new DirectoryInfo(voicesDir);
                        // Sum all files in all subdirectories
                        long totalBytes = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);

                        // Convert to MB and format
                        voicesFolderSize = $"{(totalBytes / 1024f / 1024f):N0} MB";
                    }
                }
                finally { isScanning = false; }
            });
        }

        // Model Selection
        var voicesDir = Path.Combine(config.GetPluginConfigDirectory(), "piper", "voices");
        if (!Directory.Exists(voicesDir)) Directory.CreateDirectory(voicesDir);

        var files = Directory.GetFiles(voicesDir, "*.onnx", SearchOption.AllDirectories);
        var allModels = files.Select(PiperModelInfo.FromPath).Where(m => m != null).ToList();
        if (cachedModels.Count > 0)
        {


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
            ImGui.SameLine();
            ImGui.Text("Voice Model Selection");
        }
        else
        {
            ImGui.TextColored(ImColor.Red, "No voice models found.");
        }

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
        ImGui.SameLine();
        if (ImGui.Button($"Open Voice Downloader##{MemoizedId.Create()}"))
        {
            showDownloader = true;
            // 2026 Best Practice: Fetch manifest on a background thread to prevent FFXIV frame drops
            Task.Run(async () =>
            {
                try
                {
                    // Ensure this returns IDictionary<string, PiperModel> or similar
                    var models = await piperBackend.GetAvailableModels();
                    // Cast or assign to your IDictionary<string, VoiceModel> field
                    remoteModels = models.ToDictionary(k => k.Key, v => (VoiceModel)v.Value);
                }
                catch (Exception ex)
                {
                    DetailedLog.Error($"Failed to fetch Piper manifest: {ex.Message}");
                }
            });
        }
        Components.Tooltip("Browse and download specific Piper voices from Hugging Face.");
        ImGui.SameLine();
        ImGui.TextDisabled($"Local Storage Used: {voicesFolderSize}");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Total disk space used by downloaded Piper voice models.");
        }
        // Render the window if active
        if (showDownloader) DrawVoiceDownloader();

        ImGui.Separator();

        ConfigComponents.ToggleUseGenderedVoicePresets($"Use gendered voices##{MemoizedId.Create()}", config);
        if (config.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("Piper", TTSBackend.Piper, config, presets);
        }
    }
    private void DrawVoiceDownloader()
    {
        ImGui.SetNextWindowSize(new global::System.Numerics.Vector2(500, 600), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Piper Voice Downloader", ref showDownloader))
        {
            if (remoteModels == null)
            {
                ImGui.Text("Fetching model list from Hugging Face...");
                ImGui.End();
                return;
            }

            ImGui.InputTextWithHint("##Search", "Search voices (e.g. 'en_US' or 'medium')...", ref searchQuery, 64);

            ImGui.BeginChild("ModelList", new global::System.Numerics.Vector2(0, 0), true);
            foreach (var model in remoteModels)
            {

                var entry = model.Value;
                var langName = entry.Language?.Name ?? "Unknown";
                var dataset = entry.Name ?? "Standard";
                var parts = (entry.Key ?? "unknown").Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                string quality = parts.Last().ToLower();

                string formattedName = $"{langName} : {dataset} ({quality})";

                if (!string.IsNullOrEmpty(searchQuery) && !model.Key.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check if installed
                bool isDownloaded = cachedModels.Any(m => m.FullPath.Contains(model.Key));
                bool isInstalled = cachedModels.Any(m => m.FullPath.Contains(Path.Combine("voices", model.Key)));

                ImGui.PushID(model.Key);
                ImGui.TextUnformatted(formattedName);
                ImGui.SameLine(ImGui.GetWindowWidth() - 120);

                if (isInstalled)
                {
                    ImGui.TextColored(new global::System.Numerics.Vector4(0.5f, 1f, 0.5f, 1f), "Installed");

                    // Add a small delete button to the right of the "Installed" text
                    ImGui.SameLine(ImGui.GetWindowWidth() - 40);
                    ImGui.PushStyleColor(ImGuiCol.Button, new global::System.Numerics.Vector4(0.6f, 0.2f, 0.2f, 1f)); // Reddish
                    if (ImGui.Button("X##Delete"))
                    {
                        // Trigger deletion
                        if (piperBackend.DeleteVoiceModel(model.Key))
                        {
                            // Force a re-scan of the local files immediately so the UI updates
                            lastScan = DateTime.MinValue;
                        }
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete this voice model from your computer.");
                }
                else
                {
                    if (ImGui.Button("Download"))
                    {
                        _ = piperBackend.DownloadSpecificModel(model.Key, (VoiceModel)model.Value);
                    }
                }
                ImGui.Separator();
                ImGui.PopID();
            }
            ImGui.EndChild();
            ImGui.End();
        }

    }
    public bool DeleteVoiceModel(string modelKey)
    {
        try
        {
            // 1. Kill any active speech to unlock files
            piperBackend.KillActiveProcessInternal();

            string voicesDir = piperBackend.GetVoicesDir(config);
            string modelTargetDir = Path.Combine(voicesDir, modelKey);

            if (Directory.Exists(modelTargetDir))
            {
                // Delete the folder and all contents (.onnx, .json)
                Directory.Delete(modelTargetDir, true);
                DetailedLog.Info($"Deleted voice model: {modelKey}");
                return true;
            }
        }
        catch (Exception ex)
        {
            DetailedLog.Error($"Failed to delete {modelKey}: {ex.Message}");
        }
        return false;
    }
}