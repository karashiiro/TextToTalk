using System;
using System.Linq;
using ImGuiNET;
using KokoroSharp;
using KokoroSharp.Core;
using TextToTalk.UI;

namespace TextToTalk.Backends.Kokoro;

public class KokoroBackendUI(PluginConfiguration config, KokoroBackend kokoroBackend)
{
    public void DrawVoicePresetOptions()
    {
        ImGui.TextColored(ImColor.HintColor, "This backend may cause performance issues on some systems.");

        ImGui.Spacing();

        var currentVoicePreset = config.GetCurrentVoicePreset<KokoroVoicePreset>();
        var presets = config.GetVoicePresetsForBackend(TTSBackend.Kokoro).ToList();

        if (presets.Count > 0)
        {
            var presetIndex = currentVoicePreset is not null ? presets.IndexOf(currentVoicePreset) : -1;
            if (ImGui.Combo($"Voice preset##{MemoizedId.Create()}", ref presetIndex,
                    presets.Select(p => p.Name).ToArray(), presets.Count))
            {
                config.SetCurrentVoicePreset(presets[presetIndex].Id);
                config.Save();
            }
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<KokoroVoicePreset>($"New preset##{MemoizedId.Create()}", config);

        if (presets.Count == 0 || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        BackendUI.DeletePresetButton($"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.Kokoro,
            config);

        ImGui.Separator();

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            config.Save();
        }

        var voices = KokoroVoiceManager.GetVoices(Enum.GetValues<KokoroLanguage>());
        if (ImGui.BeginCombo($"Voice##{MemoizedId.Create()}", currentVoicePreset.InternalName))
        {
            foreach (var voice in voices)
            {
                if (!ImGui.Selectable(voice.Name, voice.Name == currentVoicePreset.InternalName)) continue;
                currentVoicePreset.InternalName = voice.Name;
                config.Save();
            }

            ImGui.EndCombo();
        }

        Components.Tooltip(
            "The built-in Kokoro voice to use for this preset. The first letter is the language/accent:\na - American, b - British, j - Japanese, ect.\nThe second latter is the gender:\nf - Female, m - Male");

        var speed = currentVoicePreset.Speed ?? 1f;
        if (ImGui.SliderFloat($"Speed##{MemoizedId.Create()}", ref speed, 0.5f, 2f, "%.2fx"))
        {
            currentVoicePreset.Speed = speed;
            config.Save();
        }

        if (ImGui.Button($"Test##{MemoizedId.Create()}"))
        {
            var voice = voices.FirstOrDefault(v => v.Name == currentVoicePreset.InternalName);
            if (voice is not null)
            {
                kokoroBackend.CancelSay(TextSource.Chat);
                kokoroBackend.Say($"Hello, my name is {presetName} and I am a TTS voice preset.", currentVoicePreset,
                    TextSource.Chat, Dalamud.Game.ClientLanguage.English);
            }
        }

        Components.Tooltip("Plays a test message using the current voice preset.");

        ImGui.Separator();

        ConfigComponents.ToggleKokoroUseAmericanEnglish($"Use American English##{MemoizedId.Create()}", config);
        ConfigComponents.ToggleUseGenderedVoicePresets($"Use gendered voices##{MemoizedId.Create()}", config);
        ImGui.Spacing();
        if (config.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("Kokoro", TTSBackend.Kokoro, config, presets);
        }
    }
}