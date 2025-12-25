using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Text;
using System;
using System.Linq;
using TextToTalk.UI;
using TextToTalk.UI.GoogleCloud;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudBackendUI
{
    private readonly PluginConfiguration config;
    private readonly GoogleCloudClient client;
    private readonly CredentialsComponent credentialsComponent;
    private readonly GoogleCloudBackend backend;

    public GoogleCloudBackendUI(PluginConfiguration config, GoogleCloudClient client, GoogleCloudBackend backend)
    {
        this.backend = backend;
        this.client = client;
        this.config = config;
        this.credentialsComponent = new CredentialsComponent(client, config);
    }

    public void DrawLoginOptions()
    {
        credentialsComponent.Draw();
    }

    public void DrawVoicePresetOptions()
    {
        var currentVoicePreset = config.GetCurrentVoicePreset<GoogleCloudVoicePreset>();
        var presets = config.GetVoicePresetsForBackend(TTSBackend.GoogleCloud).ToList();

        if (presets.Count > 0 && currentVoicePreset != null)
        {
            var currentPresetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo($"Voice preset##{MemoizedId.Create()}", ref currentPresetIndex,
                    presets.Select(p => p.Name).ToArray(), presets.Count))
                config.SetCurrentVoicePreset(presets[currentPresetIndex].Id);
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<GoogleCloudVoicePreset>($"New preset##{MemoizedId.Create()}", config);

        if (presets.Count == 0 || currentVoicePreset is null) return;

        ImGui.SameLine();
        BackendUI.DeletePresetButton($"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.GoogleCloud,
            config);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            config.Save();
        }

        var localeNames = client.Locales;
        if (ImGui.BeginCombo($"Locale##{MemoizedId.Create()}", currentVoicePreset.Locale))
        {
            if (localeNames != null)
                foreach (var localeName in localeNames)
                {
                    if (!ImGui.Selectable(localeName, localeName == currentVoicePreset.Locale))
                        continue;

                    currentVoicePreset.Locale = localeName;
                    config.Save();
                }

            ImGui.EndCombo();
        }

        var voiceNames = client.Voices;
        if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", currentVoicePreset.VoiceName))
        {
            if (voiceNames != null && currentVoicePreset.Locale != null)
            {
                voiceNames = voiceNames.Where(vn => vn.StartsWith(currentVoicePreset.Locale)).ToList();
                foreach (var voiceName in voiceNames)
                {
                    if (!ImGui.Selectable(voiceName, voiceName == currentVoicePreset.VoiceName)) continue;
                    currentVoicePreset.VoiceName = voiceName;
                    config.Save();
                }
            }

            ImGui.EndCombo();
        }

        var validSampleRates = new[] { "8000", "16000", "22050", "24000" };
        var sampleRate = currentVoicePreset.SampleRate.ToString();
        var sampleRateIndex = Array.IndexOf(validSampleRates, sampleRate);
        if (ImGui.Combo($"Sample rate##{MemoizedId.Create()}", ref sampleRateIndex, validSampleRates,
                validSampleRates.Length))
        {
            currentVoicePreset.SampleRate = int.Parse(validSampleRates[sampleRateIndex]);
            this.config.Save();
        }

        var pitch = currentVoicePreset.Pitch ?? 0;
        if (ImGui.SliderFloat($"Pitch##{MemoizedId.Create()}", ref pitch, -10f, 10f, "%.2fx"))
        {
            currentVoicePreset.Pitch = pitch;
            config.Save();
        }

        var playbackRate = currentVoicePreset.PlaybackRate ?? 1;
        if (ImGui.SliderFloat($"Playback rate##{MemoizedId.Create()}", ref playbackRate, 0.25f, 4f, "%.2fx"))
        {
            currentVoicePreset.PlaybackRate = playbackRate;
            config.Save();
        }

        var volume = (int)(currentVoicePreset.Volume * 100);
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 200, "%d%%"))
        {
            currentVoicePreset.Volume = (float)Math.Round(volume / 100f, 2);
            config.Save();
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
                    Text = $"Hello from Google Cloud, this is a test message",
                    TextTemplate = "Hello from Google Cloud, this is a test message",
                    Race = "Hyur",
                    BodyType = GameEnums.BodyType.Adult,
                    ChatType = XivChatType.Say,
                    Language = ClientLanguage.English,
                };
                backend.CancelSay(TextSource.Chat);
                backend.Say(request);
            }
        }

        ConfigComponents.ToggleUseGenderedVoicePresets($"Use gendered voices##{MemoizedId.Create()}", config);
        ImGui.Spacing();
        if (config.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("GoogleCloud", TTSBackend.GoogleCloud, config, presets);
        }
    }
}