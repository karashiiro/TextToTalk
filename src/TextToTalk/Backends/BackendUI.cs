using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace TextToTalk.Backends;

public static class BackendUI
{
    public static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
    public static readonly Vector4 Red = new(1, 0, 0, 1);
    
    public static void ImGuiVoiceNotSupported()
    {
        ImGui.TextColored(Red, "Voice not supported on this engine");
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