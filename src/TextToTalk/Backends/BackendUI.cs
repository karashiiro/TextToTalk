using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using TextToTalk.UI;
using TextToTalk.UI.Windows;

namespace TextToTalk.Backends;

public static class BackendUI
{
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
        Components.HelpTooltip("""
            By default, NPCs in the game only have genders of 0 and 1, regardless of their canonical gender (or lack thereof).
            As such, any ungendered characters need to be specified by us in order to be properly reflected in-game.
            See https://github.com/karashiiro/TextToTalk/wiki/Adding-NPCs-to-the-Ungendered-Overrides-List for more information.
            """);

        if (!ungenderedVoices.Any())
        {
            ImGui.TextColored(ImColor.Red, "No ungendered voice preset(s) are selected.");
        }

        if (ImGuiPresetCombo($"Male preset(s)##{MemoizedId.Create(uniq: uniq)}", maleVoices, presets))
        {
            config.Save();
        }

        if (!maleVoices.Any())
        {
            ImGui.TextColored(ImColor.Red, "No male voice preset(s) are selected.");
        }

        if (ImGuiPresetCombo($"Female preset(s)##{MemoizedId.Create(uniq: uniq)}", femaleVoices, presets))
        {
            config.Save();
        }

        if (!femaleVoices.Any())
        {
            ImGui.TextColored(ImColor.Red, "No female voice preset(s) are selected.");
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

            // Use TryGetValue to safely access the inner dictionary for the specific backend
            if (voiceConfig.UngenderedVoicePresets.TryGetValue(backend, out var ungendered))
            {
                ungendered.Remove(preset.Id);
            }

            if (voiceConfig.MaleVoicePresets.TryGetValue(backend, out var male))
            {
                male.Remove(preset.Id);
            }

            if (voiceConfig.FemaleVoicePresets.TryGetValue(backend, out var female))
            {
                female.Remove(preset.Id);
            }

            voiceConfig.VoicePresets.Remove(preset);

            config.Save();
        }
    }

    public static void ImGuiVoiceNotSupported()
    {
        ImGui.TextColored(ImColor.Red, "Voice not supported on this engine");
    }

    public static void ImGuiVoiceNotSelected()
    {
        ImGui.TextColored(ImColor.Red, "No voice selected");
    }

    public static void ImGuiMultiVoiceHint()
    {
        ImGui.TextColored(ImColor.HintColor,
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
    public static bool ImGuiStylesCombo(string label, string previewText, SortedSet<int> selectedIndices, List<string> styles)
    {
        // Use the passed-in string, or a placeholder if it's empty
        string displayValue = !string.IsNullOrEmpty(previewText) ? previewText : "None selected";

        bool didChange = false;

        // The second parameter of BeginCombo controls what is shown in the closed box
        if (ImGui.BeginCombo(label, displayValue))
        {
            for (int i = 0; i < styles.Count; i++)
            {
                bool isSelected = selectedIndices.Contains(i);

                // Use Selectable with DontClosePopups for multi-select
                if (ImGui.Selectable(styles[i], isSelected, ImGuiSelectableFlags.DontClosePopups))
                {
                    if (!isSelected)
                        selectedIndices.Add(i);
                    else
                        selectedIndices.Remove(i);

                    didChange = true;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        return didChange;
    }
}