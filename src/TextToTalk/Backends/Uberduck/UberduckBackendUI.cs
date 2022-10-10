using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace TextToTalk.Backends.Uberduck;

public class UberduckBackendUI
{
    private static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly PluginConfiguration config;
    private readonly UberduckClient uberduck;
    private readonly Func<IList<UberduckVoice>> getVoices;

    private string apiKey = string.Empty;
    private string apiSecret = string.Empty;

    public UberduckBackendUI(PluginConfiguration config, UberduckClient uberduck,
        Func<IList<UberduckVoice>> getVoices)
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
        ImGui.TextColored(HintColor, "TTS may be delayed due to rate-limiting.");
        ImGui.Spacing();

        ImGui.InputTextWithHint("##TTTUberduckAPIKey", "API key", ref this.apiKey, 100, ImGuiInputTextFlags.Password);
        ImGui.InputTextWithHint("##TTTUberduckAPISecret", "API secret", ref this.apiSecret, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button("Save and Login##TTTSaveUberduckAuth"))
        {
            var username = Whitespace.Replace(this.apiKey, "");
            var password = Whitespace.Replace(this.apiSecret, "");
            UberduckCredentialManager.SaveCredentials(username, password);
            this.uberduck.ApiKey = username;
            this.uberduck.ApiSecret = password;
        }

        ImGui.SameLine();
        if (ImGui.Button("Register##TTTRegisterUberduckAuth"))
        {
            WebBrowser.Open("https://uberduck.ai/");
        }

        ImGui.TextColored(HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var currentVoicePreset = this.config.GetCurrentVoicePreset<UberduckVoicePreset>();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.Uberduck).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any())
        {
            var presetIndex = currentVoicePreset is not null ? presets.IndexOf(currentVoicePreset) : -1;
            if (ImGui.Combo("Preset##TTTVoice3", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                    presets.Count))
            {
                this.config.CurrentVoicePreset[TTSBackend.Uberduck] = presets[presetIndex].Id;
                this.config.Save();
            }
        }
        else
        {
            ImGui.TextColored(Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        if (ImGui.Button("New preset##TTTVoice4") &&
            this.config.TryCreateVoicePreset<UberduckVoicePreset>(out var newPreset))
        {
            this.config.SetCurrentVoicePreset(newPreset.Id);
        }

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Delete preset##TTTVoice5"))
        {
            var otherPreset = this.config.VoicePresets.First(p => p.Id != currentVoicePreset.Id);
            this.config.SetCurrentVoicePreset(otherPreset.Id);

            if (this.config.UngenderedVoicePreset[TTSBackend.Uberduck] == currentVoicePreset.Id)
            {
                this.config.UngenderedVoicePreset[TTSBackend.Uberduck] = 0;
            }
            else if (this.config.MaleVoicePreset[TTSBackend.Uberduck] == currentVoicePreset.Id)
            {
                this.config.MaleVoicePreset[TTSBackend.Uberduck] = 0;
            }
            else if (this.config.FemaleVoicePreset[TTSBackend.Uberduck] == currentVoicePreset.Id)
            {
                this.config.FemaleVoicePreset[TTSBackend.Uberduck] = 0;
            }

            this.config.VoicePresets.Remove(currentVoicePreset);
        }

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText("Preset name", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        var playbackRate = currentVoicePreset.PlaybackRate;
        if (ImGui.SliderInt("Playback rate##TTTVoice8", ref playbackRate, 20, 200, "%d%%",
                ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.PlaybackRate = playbackRate;
            this.config.Save();
        }

        var volume = (int)(currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt("Volume##TTTVoice7", ref volume, 0, 100))
        {
            currentVoicePreset.Volume = (float)Math.Round((double)volume / 100, 2);
            this.config.Save();
        }

        ImGui.Text("Lexicons");
        ImGui.TextColored(HintColor, "Lexicons are not supported on the Uberduck backend.");

        ImGui.Spacing();

        {
            var voices = this.getVoices.Invoke();
            var voiceArray = voices.Select(v => v.DisplayName).ToArray();
            var voiceIdArray = voices.Select(v => v.Name).ToArray();

            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox("Use gendered voices##TTTVoice2", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            ImGui.Spacing();
            if (useGenderedVoicePresets)
            {
                if (voices.Count == 0)
                {
                    ImGui.TextColored(Red, "No voices were found. This might indicate a temporary service outage.");
                }

                var currentUngenderedVoice = this.config.GetCurrentUngenderedVoicePreset<UberduckVoicePreset>();
                var currentMaleVoice = this.config.GetCurrentMaleVoicePreset<UberduckVoicePreset>();
                var currentFemaleVoice = this.config.GetCurrentFemaleVoicePreset<UberduckVoicePreset>();

                var ungenderedVoiceIndex = Array.IndexOf(voiceIdArray, currentUngenderedVoice.VoiceName);
                if (ImGui.Combo("Ungendered voice##TTTVoice5", ref ungenderedVoiceIndex, voiceArray, voices.Count))
                {
                    currentUngenderedVoice.VoiceName = voiceIdArray[ungenderedVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == currentUngenderedVoice.VoiceName))
                {
                    ImGuiVoiceNotSupported();
                }

                var maleVoiceIndex = Array.IndexOf(voiceIdArray, currentMaleVoice.VoiceName);
                if (ImGui.Combo("Male voice##TTTVoice3", ref maleVoiceIndex, voiceArray, voices.Count))
                {
                    currentMaleVoice.VoiceName = voiceIdArray[maleVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == currentMaleVoice.VoiceName))
                {
                    ImGuiVoiceNotSupported();
                }

                var femaleVoiceIndex = Array.IndexOf(voiceIdArray, currentFemaleVoice.VoiceName);
                if (ImGui.Combo("Female voice##TTTVoice4", ref femaleVoiceIndex, voiceArray, voices.Count))
                {
                    currentFemaleVoice.VoiceName = voiceIdArray[femaleVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == currentFemaleVoice.VoiceName))
                {
                    ImGuiVoiceNotSupported();
                }
            }
            else
            {
                var currentVoice = this.config.GetCurrentVoicePreset<UberduckVoicePreset>();
                var voiceIndex = Array.IndexOf(voiceIdArray, currentVoice.VoiceName);
                if (ImGui.Combo("Voice##TTTVoice1", ref voiceIndex, voiceArray, voices.Count))
                {
                    currentVoice.VoiceName = voiceIdArray[voiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == currentVoice.VoiceName))
                {
                    ImGuiVoiceNotSupported();
                }
            }
        }
    }

    private static void ImGuiVoiceNotSupported()
    {
        ImGui.TextColored(Red, "Voice not supported");
    }
}