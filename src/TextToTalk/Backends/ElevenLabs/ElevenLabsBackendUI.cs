using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Text;
using NAudio.SoundFont;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TextToTalk.UI;
using TextToTalk.UI.Windows;
using static TextToTalk.Backends.Azure.AzureClient;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsBackendUI
{
    private readonly ElevenLabsBackendUIModel model;
    private readonly PluginConfiguration config;
    private readonly ElevenLabsBackend backend;

    private string apiKey;

    public ElevenLabsBackendUI(ElevenLabsBackendUIModel model, PluginConfiguration config, ElevenLabsBackend backend)
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
            WebBrowser.Open("https://beta.elevenlabs.io/");
        }

        ImGui.TextColored(ImColor.HintColor, "Credentials secured with Windows Credential Manager");

        var loginError = this.model.ElevenLabsLoginException?.Message;
        if (loginError != null)
        {
            ImGui.TextColored(ImColor.Red, $"Failed to login: {loginError}");
        }

        ImGui.Spacing();

        // Show character quota
        if (this.model.UserSubscriptionInfo != null)
        {
            var subscription = this.model.UserSubscriptionInfo;
            var characterCount = subscription.CharacterCount;
            var characterLimit = subscription.CharacterLimit;
            var quotaReset = DateTimeOffset.FromUnixTimeSeconds(subscription.NextCharacterCountResetUnix);
            var ratio = Convert.ToSingle(characterCount) / characterLimit;
            var label = $"{characterCount}/{characterLimit}";
            ImGui.ProgressBar(ratio, Vector2.Zero, label);
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.Text("Characters used");
            ImGui.TextColored(ImColor.HintColor, $"Next quota reset: {quotaReset.ToLocalTime()}");
            ImGui.Spacing();
        }

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
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }
        else if (currentVoicePreset == null && presets.Count > 0)
        {
            config.SetCurrentVoicePreset(presets.First().Id);
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
            var voicePreviewName = voiceIndex == -1 ? "" : voiceDisplayNames[voiceIndex];
            if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", voicePreviewName))
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

            var modelDescriptions = this.model.Models;
            var modelIdList = modelDescriptions.Keys.ToList();
            var modelDescriptionsList = modelDescriptions.Values.Select(v => v.Items.First()).ToList();
            var selectedItemIndex = modelIdList.IndexOf(currentVoicePreset.ModelId);
            string modelPreviewName = "";
            if (selectedItemIndex != -1)
            {
                modelPreviewName = modelDescriptionsList[selectedItemIndex].ModelId;
            }

            if (ImGui.BeginCombo($"Models##{MemoizedId.Create()}", modelPreviewName))
            {
                for (int i = 0; i < modelDescriptionsList.Count; i++)
                {
                    var item = modelDescriptionsList[i];
                    bool isSelected = (selectedItemIndex == i);

                    ImGui.Selectable(item.ModelDescription, false, ImGuiSelectableFlags.Disabled);

                    if (ImGui.Selectable($"  {item.ModelId} || Cost Multiplier: {item.ModelRates["character_cost_multiplier"]}##{i}", isSelected))
                    {
                        currentVoicePreset.ModelId = item.ModelId;
                        // Snaps to nearest 0.5 for eleven_v3 compatibility
                        currentVoicePreset.Stability = (float)Math.Round(currentVoicePreset.Stability / 0.5f) * 0.5f;
                        this.config.Save();
                    }

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
        }

        var similarityBoost = currentVoicePreset.SimilarityBoost;
        if (ImGui.SliderFloat($"Clarity/Similarity boost##{MemoizedId.Create()}", ref similarityBoost, 0, 1,
                "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.SimilarityBoost = similarityBoost;
            this.config.Save();
        }

        var stability = currentVoicePreset.Stability;
        if (ImGui.SliderFloat($"Stability##{MemoizedId.Create()}", ref stability, 0, 1, "%.2f",
                ImGuiSliderFlags.AlwaysClamp))
        {
            if (currentVoicePreset.ModelId == "eleven_v3")
            {
                currentVoicePreset.Stability = (float)Math.Round(stability / 0.5f) * 0.5f; // eleven_v3 only supports 0.0, 0.5, 1.0, any other float values will return "Bad Request"
                this.config.Save();
            }
            else
            {
                currentVoicePreset.Stability = stability;
                this.config.Save();
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
        if (currentVoicePreset.ModelId == "eleven_v3")
        {
            var voiceStyles = config.CustomVoiceStyles.ToList();
            if (voiceStyles == null || voiceStyles.Count == 0)
            {
                ImGui.BeginDisabled();
                if (ImGui.BeginCombo("Style", "No styles have been configured"))
                {
                    ImGui.EndCombo();
                }
                ImGui.EndDisabled();
            }
            else
            {
                var style = currentVoicePreset.Style;
                voiceStyles.Insert(0, "");
                var styleIndex = voiceStyles.IndexOf(currentVoicePreset.Style ?? "");
                if (ImGui.Combo($"Voice Style##{MemoizedId.Create()}", ref styleIndex, voiceStyles, voiceStyles.Count))
                {
                    currentVoicePreset.Style = voiceStyles[styleIndex];
                    this.config.Save();
                }
            }
        }
        else 
        {
            ImGui.BeginDisabled();
            if (ImGui.BeginCombo("Style", "Voice Styles only available on model: eleven_v3"))
            {
                ImGui.EndCombo();
            }
            ImGui.EndDisabled();
            currentVoicePreset.Style = string.Empty; 
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
                    Style = currentVoicePreset.Style,
                    Speaker = "Speaker",
                    Text = $"Hello from ElevenLabs, this is a test message",
                    TextTemplate = "Hello from ElevenLabs, this is a test message",
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
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Use Tags like \"Shout\" or \"Whisper\" to direct your voices");
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