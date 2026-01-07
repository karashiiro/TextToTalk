using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI;
using TextToTalk.UI.Lexicons;
using TextToTalk.UI.Windows;
using System.Numerics;

namespace TextToTalk.Backends.Azure;

public class AzureBackendUI
{
    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly AzureBackendUIModel model;
    private readonly AzureBackend backend;

    private string region;
    private string subscriptionKey;

    public AzureBackendUI(AzureBackendUIModel model, PluginConfiguration config, LexiconManager lexiconManager,
        HttpClient http, AzureBackend backend)
    {
        this.model = model;

        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");
        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.backend = backend;
        this.config = config;
        this.lexiconComponent =
            new LexiconComponent(lexiconManager, lexiconRepository, config, () => config.AzureLexiconFiles);

        (region, subscriptionKey) = this.model.GetLoginInfo();
    }

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "Region", ref region, 100);
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "Subscription key", ref subscriptionKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            this.model.LoginWith(region, subscriptionKey);
        }

        ImGui.SameLine();
        if (ImGui.Button($"Register##{MemoizedId.Create()}"))
        {
            WebBrowser.Open(
                "https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/index-text-to-speech");
        }

        ImGui.TextColored(ImColor.HintColor, "Credentials secured with Windows Credential Manager");

        var loginError = this.model.AzureLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(ImColor.Red, $"Failed to login: {loginError}");
        }

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
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }
        else if (currentVoicePreset == null && presets.Count > 0)
        {
            config.SetCurrentVoicePreset(presets.First().Id);
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

            string?[] voiceArray = voices
                .Where(v => v != null && !string.IsNullOrEmpty(v.ShortName))
                .Select(v => v.ShortName)
                .ToArray();

            string[] displayArray = voices
                .Where(v => v != null && !string.IsNullOrEmpty(v.ShortName))
                .Select(v => v.Styles?.Count > 1
                             ? $"{v.ShortName} [Styles Available]"
                             : v.ShortName!)
                .ToArray();

            var voiceIndex = Array.IndexOf(voiceArray, currentVoicePreset.VoiceName);
            // 1. Determine if the currently selected voice has styles
            bool previewHasStyles = voiceIndex >= 0 && voices[voiceIndex].Styles?.Count > 1;
            string previewName = voiceIndex >= 0 ? voiceArray[voiceIndex] : "Select a voice...";

            // 2. Start combo with an empty preview string so we can draw our own
            if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", "", ImGuiComboFlags.HeightLarge))
            {
                var filteredVoices = voices.Where(v => v != null && !string.IsNullOrEmpty(v.ShortName)).ToList();

                for (int i = 0; i < filteredVoices.Count; i++)
                {
                    var v = filteredVoices[i];
                    bool isSelected = (voiceIndex == i);
                    bool hasStyles = v.Styles?.Count > 1;

                    if (ImGui.Selectable($"##{v.ShortName}_{i}", isSelected))
                    {
                        voiceIndex = i;
                        currentVoicePreset.VoiceName = voiceArray[voiceIndex];
                        this.config.Save();
                    }

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().ItemSpacing.X);
                    ImGui.Text(v.ShortName);

                    if (hasStyles)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1.0f, 1.0f), "[Styles Available]");
                    }

                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            // 3. Overlay the custom text on the Combo box itself
            // We calculate the position relative to the last item (the Combo box)
            ImGui.SameLine();
            float comboRectMinX = ImGui.GetItemRectMin().X;
            float comboRectMinY = ImGui.GetItemRectMin().Y;
            float stylePadding = ImGui.GetStyle().FramePadding.X;

            // Move cursor to inside the combo box frame
            ImGui.SetCursorScreenPos(new Vector2(comboRectMinX + stylePadding, comboRectMinY + ImGui.GetStyle().FramePadding.Y - 3.0f));

            // Draw the Name
            ImGui.Text(previewName);

            // Draw the Tag if applicable
            if (previewHasStyles)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.55f, 0.75f, 1.0f, 1.0f), "[Styles Available]");
            }
            switch (voices.Count)
            {
                case 0:
                    ImGui.TextColored(ImColor.Red,
                        "No voices are available on this voice engine for the current region.\n" +
                        "Please log in using a different region.");
                    break;
                case > 0 when !voiceArray.Any(v => v == currentVoicePreset.VoiceName):
                    BackendUI.ImGuiVoiceNotSelected();
                    break;
            }
        }

        var playbackRate = 100 + currentVoicePreset.PlaybackRate;
        if (ImGui.SliderInt($"Playback rate##{MemoizedId.Create()}", ref playbackRate, 50, 200, "%d%%",
                ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.PlaybackRate = playbackRate - 100;
            this.config.Save();
        }

        var volume = (int)(currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float)Math.Round((double)volume / 100, 2);
            this.config.Save();
        }

        var voiceStyles = new List<string>();
        var voiceDetails = this.backend?.voices?.OrderBy(v => v.ShortName).FirstOrDefault(v => v?.ShortName == currentVoicePreset?.VoiceName);
        // the styles list will always contain at least 1 empty string if there are no styles available
        if (voiceStyles == null || (voiceDetails?.Styles?.Count ?? 0) == 1) 
        {
            ImGui.BeginDisabled();
            if (ImGui.BeginCombo("Style", "No styles available for this voice"))
            {
                ImGui.EndCombo();
            }

            ImGui.EndDisabled();
        }
        else if (voiceDetails?.Styles != null && voiceDetails.Styles.Count > 0)
        {
            voiceStyles.Add("");
            voiceStyles.AddRange(voiceDetails.Styles);
            var styleIndex = voiceStyles.IndexOf(currentVoicePreset.Style ?? "");
            if (ImGui.Combo($"Style##{MemoizedId.Create()}", ref styleIndex, voiceStyles, voiceStyles.Count))
            {
                currentVoicePreset.Style = voiceStyles[styleIndex];
                this.config.Save();
            }
        }
        ImGui.Separator();

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
                    Text = $"Hello from Azure Cognitive Services, this is a test message",
                    TextTemplate = "Hello from Azure Cognitive Services, this is a test message",
                    Race = "Hyur",
                    BodyType = GameEnums.BodyType.Adult,
                    ChatType = XivChatType.Say,
                    Language = ClientLanguage.English,
                };
                backend.CancelSay(TextSource.Chat);
                backend.Say(request);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button($"Configure Voice Styles##{MemoizedId.Create()}"))
        {
            VoiceStyles.Instance?.ToggleStyle();
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
                BackendUI.GenderedPresetConfig("Azure", TTSBackend.Azure, this.config, presets);
            }
        }
    }
}