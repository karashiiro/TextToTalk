using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using TextToTalk.UI;

namespace TextToTalk.Backends;

public static class BackendUI
{
    public static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
    public static readonly Vector4 Red = new(1, 0, 0, 1);

    public static void GenderedPresetConfig(string uniq, TTSBackend backend, PluginConfiguration config,
        List<VoicePreset> presets)
    {
        var voiceConfig = config.GetVoiceConfig();
        var ungenderedVoices = voiceConfig.GetUngenderedPresets(backend);
        var maleVoices = voiceConfig.GetMalePresets(backend);
        var femaleVoices = voiceConfig.GetFemalePresets(backend);

        if (ImGuiPresetCombo($"Ungendered preset(s)##{MemoizedId.Create(uniq: uniq)}", ungenderedVoices, presets))
        {
            config.Save();
        }

        if (!ungenderedVoices.Any())
        {
            ImGui.TextColored(Red, "No ungendered voice preset(s) are selected.");
        }

        if (ImGuiPresetCombo($"Male preset(s)##{MemoizedId.Create(uniq: uniq)}", maleVoices, presets))
        {
            config.Save();
        }

        if (!maleVoices.Any())
        {
            ImGui.TextColored(Red, "No male voice preset(s) are selected.");
        }

        if (ImGuiPresetCombo($"Female preset(s)##{MemoizedId.Create(uniq: uniq)}", femaleVoices, presets))
        {
            config.Save();
        }

        if (!femaleVoices.Any())
        {
            ImGui.TextColored(Red, "No female voice preset(s) are selected.");
        }

        ImGuiMultiVoiceHint();
    }

    public static void NewPresetButton<TPreset>(string label, PluginConfiguration config)
        where TPreset : VoicePreset, new()
    {
        if (ImGui.Button(label) && config.TryCreateVoicePreset<TPreset>(out var newPreset))
        {
            config.SetCurrentVoicePreset(newPreset.Id);
            config.Save();
        }
    }

    public static void DeletePresetButton(string label, VoicePreset preset, TTSBackend backend,
        PluginConfiguration config)
    {
        if (ImGui.Button(label))
        {
            var voiceConfig = config.GetVoiceConfig();

            var otherPreset = voiceConfig.VoicePresets.First(p => p.Id != preset.Id);
            config.SetCurrentVoicePreset(otherPreset.Id);

            voiceConfig.UngenderedVoicePresets[backend].Remove(preset.Id);
            voiceConfig.MaleVoicePresets[backend].Remove(preset.Id);
            voiceConfig.FemaleVoicePresets[backend].Remove(preset.Id);

            voiceConfig.VoicePresets.Remove(preset);

            config.Save();
        }
    }

    public static void ImGuiVoiceNotSupported()
    {
        ImGui.TextColored(Red, "Voice not supported on this engine");
    }

    public static void ImGuiVoiceNotSelected()
    {
        ImGui.TextColored(Red, "No voice selected");
    }

    public static void ImGuiMultiVoiceHint()
    {
        ImGui.TextColored(HintColor,
            "If multiple presets are selected per gender, they will be randomly assigned to characters.");
    }

    public static bool ImGuiPresetCombo(string label, SortedSet<int> selectedPresets, List<VoicePreset> presets)
    {
        var selectedPresetNames =
            presets.Where(preset => selectedPresets.Contains(preset.Id)).Select(preset => preset.Name);
        if (!ImGui.BeginCombo(label, string.Join(", ", selectedPresetNames)))
        {
            return false;
        }

        var didPresetsChange = false;

        foreach (var preset in presets)
        {
            var isPresetSelected = selectedPresets.Contains(preset.Id);
            if (ImGui.Selectable(preset.Name, ref isPresetSelected, ImGuiSelectableFlags.DontClosePopups))
            {
                if (isPresetSelected)
                {
                    selectedPresets.Add(preset.Id);
                }
                else
                {
                    selectedPresets.Remove(preset.Id);
                }

                didPresetsChange = true;
            }
        }

        ImGui.EndCombo();
        return didPresetsChange;
    }
}