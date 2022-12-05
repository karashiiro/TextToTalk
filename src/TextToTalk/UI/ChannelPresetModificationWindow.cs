using System.Linq;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TextToTalk.UI
{
    public class ChannelPresetModificationWindow : Window
    {
        private readonly PluginConfiguration config;
        
        public ChannelPresetModificationWindow(PluginConfiguration config) : base("Preset##TTT5", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
        {
            this.config = config;
        }

        public override void Draw()
        {
            var preset = this.config.GetCurrentEnabledChatTypesPreset();

            var presetName = preset.Name;
            if (ImGui.InputText("Name##TTT4", ref presetName, 200))
            {
                preset.Name = presetName;
                this.config.Save();
            }

            var useKeybind = preset.UseKeybind;
            if (ImGui.Checkbox("Enable Keybind", ref useKeybind))
            {
                preset.UseKeybind = useKeybind;
                this.config.Save();
            }

            if (useKeybind)
            {
                ImGui.PushItemWidth(100f);
                var kItem1 = VirtualKey.EnumToIndex(preset.ModifierKey);
                if (ImGui.Combo("##PresetKeybind1", ref kItem1, VirtualKey.Names.Take(3).ToArray(), 3))
                {
                    preset.ModifierKey = VirtualKey.IndexToEnum(kItem1);
                    this.config.Save();
                }
                ImGui.SameLine();
                var kItem2 = VirtualKey.EnumToIndex(preset.MajorKey) - 3;
                if (ImGui.Combo("Preset Enable Keybind##PresetKeybind2", ref kItem2, VirtualKey.Names.Skip(3).ToArray(), VirtualKey.Names.Length - 3))
                {
                    preset.MajorKey = VirtualKey.IndexToEnum(kItem2 + 3);
                    this.config.Save();
                }
                ImGui.PopItemWidth();
            }

            ImGui.Spacing();

            if (ImGui.Button("Close###TTT6"))
            {
                IsOpen = false;
            }
        }
    }
}