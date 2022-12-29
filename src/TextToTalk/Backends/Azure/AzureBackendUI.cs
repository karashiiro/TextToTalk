using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using ImGuiNET;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureBackendUI
{
    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly LexiconManager lexiconManager;

    private readonly Func<AzureClient> getAzure;
    private readonly Action<AzureClient> setAzure;
    private readonly Func<IList<string>> getVoices;
    private readonly Action<IList<string>> setVoices;

    private string region = string.Empty;
    private string subscriptionKey = string.Empty;

    public AzureBackendUI(PluginConfiguration config, LexiconManager lexiconManager, HttpClient http,
        Func<AzureClient> getAzure, Action<AzureClient> setAzure, Func<IList<string>> getVoices,
        Action<IList<string>> setVoices)
    {
        this.getAzure = getAzure;
        this.setAzure = setAzure;
        this.getVoices = getVoices;
        this.setVoices = setVoices;

        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");
        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.config = config;
        this.lexiconComponent =
            new LexiconComponent(lexiconManager, lexiconRepository, config, Array.Empty<string>);
        this.lexiconManager = lexiconManager;

        var credentials = AzureCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.region = credentials.UserName;
            this.subscriptionKey = credentials.Password;

            AzureLogin();
        }
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        ImGui.InputTextWithHint("##TTTAzureRegion", "Region", ref this.region, 100);
        ImGui.InputTextWithHint("##TTTAzureSubscriptionKey", "Subscription key", ref this.subscriptionKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button("Save and Login##TTTSaveAzureAuth"))
        {
            this.region = Whitespace.Replace(this.region, "");
            this.subscriptionKey = Whitespace.Replace(this.subscriptionKey, "");
            AzureCredentialManager.SaveCredentials(this.region, this.subscriptionKey);

            AzureLogin();
        }

        ImGui.SameLine();
        if (ImGui.Button("Register##TTTRegisterAzureAuth"))
        {
            WebBrowser.Open(
                "https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/index-text-to-speech");
        }

        ImGui.TextColored(BackendUI.HintColor, "Credentials secured with Windows Credential Manager");

        ImGui.Spacing();

        var currentVoicePreset = this.config.GetCurrentVoicePreset<AzureVoicePreset>();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.Azure).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any())
        {
            var presetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo("Preset##TTTAzurePresetSelect", ref presetIndex, presets.Select(p => p.Name).ToArray(),
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

        if (ImGui.Button("New preset##TTTAzureVoice4") &&
            this.config.TryCreateVoicePreset<AzureVoicePreset>(out var newPreset))
        {
            this.config.SetCurrentVoicePreset(newPreset.Id);
        }

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Delete preset##TTTAzureVoice5"))
        {
            var voiceConfig = this.config.GetVoiceConfig();
            
            var otherPreset = voiceConfig.VoicePresets.FirstOrDefault(
                p => p.Id != currentVoicePreset.Id && p.EnabledBackend == TTSBackend.Azure);
            this.config.SetCurrentVoicePreset(otherPreset?.Id ?? 0);

            voiceConfig.UngenderedVoicePresets[TTSBackend.Azure].Remove(currentVoicePreset.Id);
            voiceConfig.MaleVoicePresets[TTSBackend.Azure].Remove(currentVoicePreset.Id);
            voiceConfig.FemaleVoicePresets[TTSBackend.Azure].Remove(currentVoicePreset.Id);

            voiceConfig.VoicePresets.Remove(currentVoicePreset);
        }

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText("Preset name##TTTAzureVoice99", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        {
            var voices = this.getVoices.Invoke();
            var voiceArray = voices.ToArray();
            var voiceIndex = Array.IndexOf(voiceArray, currentVoicePreset.VoiceName);
            if (ImGui.Combo("Voice##TTTAzureVoice98", ref voiceIndex, voiceArray, voices.Count))
            {
                currentVoicePreset.VoiceName = voiceArray[voiceIndex];
                this.config.Save();
            }

            switch (voices.Count)
            {
                case 0:
                    ImGui.TextColored(BackendUI.Red,
                        "No voices are available on this voice engine for the current region.\n" +
                        "Please log in using a different region.");
                    break;
                case > 0 when !voices.Any(v => v == currentVoicePreset.VoiceName):
                    BackendUI.ImGuiVoiceNotSelected();
                    break;
            }
        }

        var playbackRate = currentVoicePreset.PlaybackRate;
        if (ImGui.SliderInt("Playback rate##TTTAzureVoice8", ref playbackRate, 20, 200, "%d%%",
                ImGuiSliderFlags.AlwaysClamp))
        {
            currentVoicePreset.PlaybackRate = playbackRate;
            this.config.Save();
        }

        var volume = (int)(currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt("Volume##TTTAzureVoice7", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float)Math.Round((double)volume / 100, 2);
            this.config.Save();
        }

        this.lexiconComponent.Draw();
        ImGui.Spacing();

        {
            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox("Use gendered voices##TTTAzureVoice2", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            ImGui.Spacing();
            if (useGenderedVoicePresets)
            {
                var voiceConfig = this.config.GetVoiceConfig();
                
                if (BackendUI.ImGuiPresetCombo("Ungendered preset(s)##TTTAzureEnabledUPresetSelect",
                        voiceConfig.GetUngenderedPresets(TTSBackend.Azure), presets))
                {
                    this.config.Save();
                }

                if (BackendUI.ImGuiPresetCombo("Male preset(s)##TTTAzureEnabledMPresetSelect",
                        voiceConfig.GetMalePresets(TTSBackend.Azure), presets))
                {
                    this.config.Save();
                }

                if (BackendUI.ImGuiPresetCombo("Female preset(s)##TTTAzureEnabledFPresetSelect",
                        voiceConfig.GetFemalePresets(TTSBackend.Azure), presets))
                {
                    this.config.Save();
                }

                BackendUI.ImGuiMultiVoiceHint();
            }
        }
    }

    private void AzureLogin()
    {
        var azure = this.getAzure.Invoke();
        azure?.Dispose();
        try
        {
            PluginLog.Log($"Logging into Azure region {region}.");
            azure = new AzureClient(this.subscriptionKey, this.region, this.lexiconManager);
            var voices = azure.GetVoices();
            this.setAzure.Invoke(azure);
            this.setVoices.Invoke(voices);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Failed to initialize Azure client.");
            AzureCredentialManager.DeleteCredentials();
        }
    }
}