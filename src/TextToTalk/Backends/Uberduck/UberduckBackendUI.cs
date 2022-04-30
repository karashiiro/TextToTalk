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
        ImGui.InputTextWithHint("##TTTUberduckAPISecret", "API secret", ref this.apiSecret, 100, ImGuiInputTextFlags.Password);

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

        var playbackRate = this.config.UberduckPlaybackRate;
        if (ImGui.SliderInt("Playback rate##TTTVoice8", ref playbackRate, 20, 200, "%d%%",
                ImGuiSliderFlags.AlwaysClamp))
        {
            this.config.UberduckPlaybackRate = playbackRate;
            this.config.Save();
        }

        var volume = (int)(this.config.UberduckVolume * 100);
        if (ImGui.SliderInt("Volume##TTTVoice7", ref volume, 0, 100))
        {
            this.config.UberduckVolume = (float)Math.Round((double)volume / 100, 2);
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

                var currentUngenderedVoiceId = this.config.UberduckVoiceUngendered;
                var currentMaleVoiceId = this.config.UberduckVoiceMale;
                var currentFemaleVoiceId = this.config.UberduckVoiceFemale;

                var ungenderedVoiceIndex = Array.IndexOf(voiceIdArray, currentUngenderedVoiceId);
                if (ImGui.Combo("Ungendered voice##TTTVoice5", ref ungenderedVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.UberduckVoiceUngendered = voiceIdArray[ungenderedVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == this.config.UberduckVoiceUngendered))
                {
                    ImGuiVoiceNotSupported();
                }

                var maleVoiceIndex = Array.IndexOf(voiceIdArray, currentMaleVoiceId);
                if (ImGui.Combo("Male voice##TTTVoice3", ref maleVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.UberduckVoiceMale = voiceIdArray[maleVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == this.config.UberduckVoiceMale))
                {
                    ImGuiVoiceNotSupported();
                }

                var femaleVoiceIndex = Array.IndexOf(voiceIdArray, currentFemaleVoiceId);
                if (ImGui.Combo("Female voice##TTTVoice4", ref femaleVoiceIndex, voiceArray, voices.Count))
                {
                    this.config.UberduckVoiceFemale = voiceIdArray[femaleVoiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == this.config.UberduckVoiceFemale))
                {
                    ImGuiVoiceNotSupported();
                }
            }
            else
            {
                var currentVoiceId = this.config.PollyVoice;
                var voiceIndex = Array.IndexOf(voiceIdArray, currentVoiceId);
                if (ImGui.Combo("Voice##TTTVoice1", ref voiceIndex, voiceArray, voices.Count))
                {
                    this.config.UberduckVoice = voiceIdArray[voiceIndex];
                    this.config.Save();
                }

                if (voices.Count > 0 && !voices.Any(v => v.Name == this.config.UberduckVoice))
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