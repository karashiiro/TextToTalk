using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TextToTalk.UI;

namespace TextToTalk.Backends.Uberduck;

public class UberduckBackendUI
{
    private readonly PluginConfiguration config;
    private readonly UberduckClient uberduck;
    private readonly Func<IDictionary<string, IList<UberduckVoice>>> getVoices;

    private string apiKey = string.Empty;
    private string apiSecret = string.Empty;

    public UberduckBackendUI(PluginConfiguration config, UberduckClient uberduck,
        Func<IDictionary<string, IList<UberduckVoice>>> getVoices)
    {
        this.config = config;
        this.uberduck = uberduck;
        this.getVoices = getVoices;

        var credentials = UberduckCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.apiKey = credentials.UserName;
            this.apiSecret = credentials.Password;
        }

        this.uberduck.ApiKey = this.apiKey;
        this.uberduck.ApiSecret = this.apiSecret;
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        ImGui.TextColored(BackendUI.HintColor, "TTS may be delayed due to rate-limiting.");
        ImGui.Spacing();

        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API key", ref this.apiKey, 100, ImGuiInputTextFlags.Password);
        ImGui.InputTextWithHint($"##{MemoizedId.Create()}", "API secret", ref this.apiSecret, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button($"Save and Login##{MemoizedId.Create()}"))
        {
            var username = Whitespace.Replace(this.apiKey, "");
            var password = Whitespace.Replace(this.apiSecret, "");
            UberduckCredentialManager.SaveCredentials(username, password);
            this.uberduck.ApiKey = username;
            this.uberduck.ApiSecret = password;
        }

        ImGui.SameLine();
        if (ImGui.Button($"Register##{MemoizedId.Create()}"))
        {
            WebBrowser.Open("https://uberduck.ai/");
        }

        ImGui.TextColored(BackendUI.HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var currentVoicePreset = this.config.GetCurrentVoicePreset<UberduckVoicePreset>();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.Uberduck).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any())
        {
            var presetIndex = currentVoicePreset is not null ? presets.IndexOf(currentVoicePreset) : -1;
            if (ImGui.Combo($"Preset##{MemoizedId.Create()}", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                    presets.Count))
            {
                this.config.SetCurrentVoicePreset(presets[presetIndex].Id);
                this.config.Save();
            }
        }
        else
        {
            ImGui.TextColored(BackendUI.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<UberduckVoicePreset>($"New preset##{MemoizedId.Create()}", this.config);

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button($"Delete preset##{MemoizedId.Create()}"))
        {
            var voiceConfig = this.config.GetVoiceConfig();

            var otherPreset = voiceConfig.VoicePresets.First(p => p.Id != currentVoicePreset.Id);
            this.config.SetCurrentVoicePreset(otherPreset.Id);

            voiceConfig.UngenderedVoicePresets[TTSBackend.Uberduck].Remove(currentVoicePreset.Id);
            voiceConfig.MaleVoicePresets[TTSBackend.Uberduck].Remove(currentVoicePreset.Id);
            voiceConfig.FemaleVoicePresets[TTSBackend.Uberduck].Remove(currentVoicePreset.Id);

            voiceConfig.VoicePresets.Remove(currentVoicePreset);
        }

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        {
            var voiceCategories = this.getVoices.Invoke();
            var voiceCategoriesFlat = voiceCategories.SelectMany(vc => vc.Value).ToList();
            var voiceDisplayNames = voiceCategoriesFlat.Select(v => v.DisplayName).ToArray();
            var voiceIds = voiceCategoriesFlat.Select(v => v.Name).ToArray();
            var voiceIndex = Array.IndexOf(voiceIds, currentVoicePreset.VoiceName);
            if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", voiceDisplayNames[voiceIndex]))
            {
                foreach (var (category, voices) in voiceCategories)
                {
                    ImGui.Selectable(category, false, ImGuiSelectableFlags.Disabled);
                    foreach (var voice in voices)
                    {
                        if (ImGui.Selectable($"  {voice.DisplayName}"))
                        {
                            currentVoicePreset.VoiceName = voice.Name;
                            this.config.Save();
                        }

                        if (voice.Name == currentVoicePreset.VoiceName)
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
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 100))
        {
            currentVoicePreset.Volume = (float)Math.Round((double)volume / 100, 2);
            this.config.Save();
        }

        ImGui.Text("Lexicons");
        ImGui.TextColored(BackendUI.HintColor, "Lexicons are not supported on the Uberduck backend.");

        ImGui.Spacing();

        {
            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox($"Use gendered voices##{MemoizedId.Create()}", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            ImGui.Spacing();
            if (useGenderedVoicePresets)
            {
                var voiceConfig = this.config.GetVoiceConfig();

                if (BackendUI.ImGuiPresetCombo($"Ungendered preset(s)##{MemoizedId.Create()}",
                        voiceConfig.GetUngenderedPresets(TTSBackend.Uberduck), presets))
                {
                    this.config.Save();
                }

                if (BackendUI.ImGuiPresetCombo($"Male preset(s)##{MemoizedId.Create()}",
                        voiceConfig.GetMalePresets(TTSBackend.Uberduck), presets))
                {
                    this.config.Save();
                }

                if (BackendUI.ImGuiPresetCombo($"Female preset(s)##{MemoizedId.Create()}",
                        voiceConfig.GetFemalePresets(TTSBackend.Uberduck), presets))
                {
                    this.config.Save();
                }

                BackendUI.ImGuiMultiVoiceHint();
            }
        }
    }
}