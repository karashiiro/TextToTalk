using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Text;
using System;
using System.Linq;
using System.Numerics;
using TextToTalk.UI;
using TextToTalk.UI.Windows;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioBackendUI
{
    private static readonly string[] Models = { "s2.1-pro-free", "s2.1-pro", "s2-pro", "s1" };
    private static readonly string[] Latencies = { "normal", "low", "balanced" };

    private readonly FishAudioBackendUIModel model;
    private readonly PluginConfiguration config;
    private readonly FishAudioBackend backend;

    private string apiKey;

    public FishAudioBackendUI(FishAudioBackendUIModel model, PluginConfiguration config, FishAudioBackend backend)
    {
        this.model = model;
        this.config = config;
        this.apiKey = this.model.GetApiKey();
        this.backend = backend;
    }

    public void DrawSettings()
    {
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API key", ref this.apiKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            this.model.LoginWith(this.apiKey);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Register##{MemoizedId.Create()}"))
        {
            WebBrowser.Open("https://fish.audio/app/api-keys");
        }

        ImGui.TextColored(ImColor.HintColor, "Credentials secured with Windows Credential Manager");

        var loginError = this.model.FishAudioLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(ImColor.Red, $"Failed to login: {loginError}");
        }

        ImGui.Spacing();

        if (this.model.ApiCreditInfo != null)
        {
            ImGui.Text($"API credit: {this.model.ApiCreditInfo.Credit}");
            ImGui.Spacing();
        }

        var currentVoicePreset = this.model.GetCurrentVoicePreset();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.FishAudio).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any() && currentVoicePreset != null)
        {
            var presetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo($"Preset##{MemoizedId.Create()}", ref presetIndex, presets.Select(p => p.Name ?? "").ToArray(),
                    presets.Count))
            {
                this.model.SetCurrentVoicePreset(presets[presetIndex].Id);
            }
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }
        else if (currentVoicePreset == null && presets.Count > 0)
        {
            config.SetCurrentVoicePreset(presets.First().Id);
        }

        BackendUI.NewPresetButton<FishAudioVoicePreset>($"New preset##{MemoizedId.Create()}", this.config);

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        BackendUI.DeletePresetButton(
            $"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.FishAudio,
            this.config);

        var presetName = currentVoicePreset.Name ?? "";
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        {
            var voiceModels = this.model.VoiceModels;
            var voiceIds = voiceModels.Select(v => v.Id ?? "").ToArray();
            var voiceIndex = Array.IndexOf(voiceIds, currentVoicePreset.VoiceId ?? "");
            var isDefault = string.IsNullOrEmpty(currentVoicePreset.VoiceId);
            var voicePreviewName = isDefault ? "Default (no voice model)" : (voiceIndex == -1 ? "" : voiceModels[voiceIndex].Title ?? "");
            if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", voicePreviewName))
            {
                if (ImGui.Selectable("Default (no voice model)", isDefault))
                {
                    currentVoicePreset.VoiceId = null;
                    this.config.Save();
                }

                if (isDefault) ImGui.SetItemDefaultFocus();

                ImGui.Separator();

                foreach (var voiceModel in voiceModels)
                {
                    if (ImGui.Selectable($"  {voiceModel.Title}"))
                    {
                        currentVoicePreset.VoiceId = voiceModel.Id;
                        this.config.Save();
                    }

                    if (voiceModel.Id == currentVoicePreset.VoiceId)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.TextColored(ImColor.HintColor, "Only your own voice models are listed; create them at fish.audio.");

            var selectedModelIndex = Array.IndexOf(Models, currentVoicePreset.ModelId ?? "");
            var modelPreview = selectedModelIndex == -1
                ? "Select a model..."
                : Models[selectedModelIndex];

            if (ImGui.BeginCombo($"Model##{MemoizedId.Create()}", modelPreview))
            {
                for (int i = 0; i < Models.Length; i++)
                {
                    var isSelected = selectedModelIndex == i;
                    if (ImGui.Selectable($"{Models[i]}##{i}", isSelected))
                    {
                        currentVoicePreset.ModelId = Models[i];
                        this.config.Save();
                    }

                    if (isSelected) ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            var selectedLatencyIndex = Array.IndexOf(Latencies, currentVoicePreset.Latency ?? "normal");
            if (selectedLatencyIndex == -1) selectedLatencyIndex = 0;
            if (ImGui.Combo($"Latency##{MemoizedId.Create()}", ref selectedLatencyIndex, Latencies, Latencies.Length))
            {
                currentVoicePreset.Latency = Latencies[selectedLatencyIndex];
                this.config.Save();
            }
        }

        var temperature = currentVoicePreset.Temperature;
        if (ImGui.SliderFloat($"Temperature##{MemoizedId.Create()}", ref temperature, 0, 1, "%.2f",
                ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.Temperature = temperature;
            this.config.Save();
        }

        var topP = currentVoicePreset.TopP;
        if (ImGui.SliderFloat($"Top P##{MemoizedId.Create()}", ref topP, 0, 1, "%.2f",
                ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.TopP = topP;
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

        if (ImGui.Button($"Test##{MemoizedId.Create()}"))
        {
            if (currentVoicePreset is not null)
            {
                var request = new SayRequest
                {
                    Source = TextSource.Chat,
                    Voice = currentVoicePreset,
                    Speaker = "Speaker",
                    Text = $"Hello from Fish Audio, this is a test message",
                    TextTemplate = $"Hello from Fish Audio, this is a test message",
                    Race = "Hyur",
                    BodyType = GameEnums.BodyType.Adult,
                    Gender = GameEnums.Gender.None,
                    ChatType = XivChatType.Say,
                    Language = ClientLanguage.English,
                };
                backend.CancelSay(TextSource.Chat);
                backend.Say(request);
            }
        }

        {
            ConfigComponents.ToggleUseGenderedVoicePresets(
                $"Use gendered voices##{MemoizedId.Create()}",
                this.config);

            ImGui.Spacing();
            if (this.config.UseGenderedVoicePresets)
            {
                BackendUI.GenderedPresetConfig("FishAudio", TTSBackend.FishAudio, this.config, presets);
            }
        }
    }
}
