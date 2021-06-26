using Dalamud.CrystalTower.UI;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace TextToTalk.UI
{
    public class PresetModificationWindow : ImmediateModeWindow
    {
        public PluginConfiguration Configuration { get; set; }

        public int PresetId { get; set; }

        public override void Draw(ref bool visible)
        {
            ImGui.SetNextWindowSize(new Vector2(320, 90));
            ImGui.Begin("Preset##TTT5", ref visible, ImGuiWindowFlags.NoResize);
            {
                var preset = Configuration.EnabledChatTypesPresets.First(p => p.Id == PresetId);

                var presetName = preset.Name;
                if (ImGui.InputText("Name##TTT4", ref presetName, 200))
                {
                    preset.Name = presetName;
                    Configuration.Save();
                }

                var useKeybind = preset.UseKeybind;
                if (ImGui.Checkbox("Enable Keybind", ref useKeybind))
                {
                    preset.UseKeybind = useKeybind;
                    Configuration.Save();
                }

                ImGui.PushItemWidth(100f);
                var kItem1 = VirtualKey.EnumToIndex(preset.ModifierKey);
                if (ImGui.Combo("##TextToTalkKeybind1", ref kItem1, VirtualKey.Names.Take(3).ToArray(), 3))
                {
                    preset.ModifierKey = VirtualKey.IndexToEnum(kItem1);
                    Configuration.Save();
                }
                ImGui.SameLine();
                var kItem2 = VirtualKey.EnumToIndex(preset.MajorKey) - 3;
                if (ImGui.Combo("TTS Toggle Keybind##TextToTalkKeybind2", ref kItem2, VirtualKey.Names.Skip(3).ToArray(), VirtualKey.Names.Length - 3))
                {
                    preset.MajorKey = VirtualKey.IndexToEnum(kItem2) + 3;
                    Configuration.Save();
                }
                ImGui.PopItemWidth();

                ImGui.Spacing();

                if (ImGui.Button("Close###TTT6"))
                {
                    visible = false;
                }
            }
            ImGui.End();
        }
    }
}