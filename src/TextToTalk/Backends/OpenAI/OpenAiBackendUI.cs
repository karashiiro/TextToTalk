using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Text;
using System;
using System.Linq;
using TextToTalk.UI;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackendUI
{
    private readonly OpenAiBackendUIModel model;
    private readonly PluginConfiguration config;
    private readonly OpenAiBackend backend;

    private string apiKey;

    public OpenAiBackendUI(OpenAiBackendUIModel model, PluginConfiguration config, OpenAiBackend backend)
    {
        this.config = config;
        this.model = model;
        this.apiKey = this.model.GetApiKey();
        this.backend = backend;
    }

    public void DrawLoginOptions()
    {
        // API key length is 132, leaving a bit extra in case the format changes
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API key", ref apiKey, 200,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            this.model.LoginWith(this.apiKey);
        }

        ImGui.TextColored(ImColor.HintColor, "Credentials secured with Windows Credential Manager");

        var loginError = this.model.OpenAiLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(ImColor.Red, $"Failed to login: {loginError}");
        }
        
    }

    public void DrawVoicePresetOptions()
    {
        var currentVoicePreset = model.GetCurrentVoicePreset();
        var presets = config.GetVoicePresetsForBackend(TTSBackend.OpenAi).ToList();

        if (presets.Count > 0 && currentVoicePreset != null)
        {
            var currentPresetIndex = presets.IndexOf(currentVoicePreset);
            var presetDisplayNames = presets
                .Select(p =>
                    {
                        if (p is OpenAiVoicePreset openAiVoicePreset)
                        {
                            return $"{openAiVoicePreset.Name} ({openAiVoicePreset.Model} - {openAiVoicePreset.VoiceName})";
                        }

                        return p.Name;
                    })
                .ToArray();
            if (ImGui.Combo($"Voice preset##{MemoizedId.Create()}", ref currentPresetIndex, presetDisplayNames, presets.Count))
                config.SetCurrentVoicePreset(presets[currentPresetIndex].Id);
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }
        else if (currentVoicePreset == null && presets.Count > 0)
        {
            config.SetCurrentVoicePreset(presets.First().Id);
        }

        BackendUI.NewPresetButton<OpenAiVoicePreset>($"New preset##{MemoizedId.Create()}", config);

        if (presets.Count == 0 || currentVoicePreset is null) return;

        ImGui.SameLine();
        BackendUI.DeletePresetButton($"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.OpenAi,
            config);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            config.Save();
        }
        
        var modelNames = OpenAiClient.Models.Select(x => x.ModelName).ToArray();
        if (currentVoicePreset.Model == null || !modelNames.Contains(currentVoicePreset.Model))
        {
            currentVoicePreset.Model = modelNames.First();
            config.Save();
        }
        
        if (ImGui.BeginCombo($"Model##{MemoizedId.Create()}", currentVoicePreset.Model))
        {
            foreach (var modelName in modelNames)
            {
                if (ImGui.Selectable(modelName, modelName == currentVoicePreset.Model))
                {
                    currentVoicePreset.Model = modelName;
                    config.Save();
                }
            }

            ImGui.EndCombo();
        }

        if (currentVoicePreset.Model == null) return;
        
        var currentModel = OpenAiClient.Models.First(x => x.ModelName == currentVoicePreset.Model);
        var voiceNames = currentModel.Voices;
        if (currentVoicePreset.VoiceName == null || !voiceNames.Contains(currentVoicePreset.VoiceName))
        {
            currentVoicePreset.VoiceName = voiceNames.First();
            config.Save();
        }
        
        if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", currentVoicePreset.VoiceName))
        {
            foreach (var voiceName in voiceNames)
            {
                if (!ImGui.Selectable(voiceName, voiceName == currentVoicePreset.VoiceName)) continue;

                currentVoicePreset.VoiceName = voiceName;
                config.Save();
            }

            ImGui.EndCombo();
        }

        var volume = (int) (currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float) Math.Round(volume / 100f, 2);
            config.Save();
        }
        
        if (currentModel.SpeedSupported)
        {
            var playbackRate = currentVoicePreset.PlaybackRate ?? 1;
            if (ImGui.SliderFloat($"Playback rate##{MemoizedId.Create()}", ref playbackRate, 0.25f, 4f, "%.2fx"))
            {
                currentVoicePreset.PlaybackRate = playbackRate;
                config.Save();
            }
        }

        if (currentModel.InstructionsSupported)
        {
            var instructions = currentVoicePreset.Instructions ?? "";
            if (ImGui.InputTextWithHint($"Instructions##{MemoizedId.Create()}",
                    "Enter instructions to direct the tone, style, pacing, pronunciation, etc.",
                    ref instructions, 1024))
            {
                currentVoicePreset.Instructions = instructions;
                config.Save();
            }

            Components.HelpTooltip("""
                Instructions are additional information that can be provided to the model to help it generate more accurate speech.
                This can include things like emphasis, pronunciation, pauses, tone, pacing, voice affect, inflections, word choice etc.
                Examples can be found at https://openai.fm
                """);
        }
        if (ImGui.Button($"Test##{MemoizedId.Create()}"))
        {
            var voice = currentVoicePreset;
            if (voice is not null)
            {
                var request = new SayRequest
                {
                    Source = TextSource.Chat,
                    Voice = currentVoicePreset,
                    Speaker = "Speaker",
                    Text = $"Hello from Open AI, this is a test message",
                    TextTemplate = "Hello from Open AI, this is a test message",
                    Race = "Hyur",
                    BodyType = GameEnums.BodyType.Adult,
                    ChatType = XivChatType.Say,
                    Language = ClientLanguage.English,
                };
                backend.CancelSay(TextSource.Chat);
                backend.Say(request);
            }
        }

        ImGui.Separator();

        ConfigComponents.ToggleUseGenderedVoicePresets($"Use gendered voices##{MemoizedId.Create()}", config);
        ImGui.Spacing();
        if (config.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("OpenAI", TTSBackend.OpenAi, config, presets);
        }
    }
}