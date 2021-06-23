using System.Linq;
using Dalamud.CrystalTower.UI;
using ImGuiNET;
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
            }
            ImGui.End();
        }
    }
}