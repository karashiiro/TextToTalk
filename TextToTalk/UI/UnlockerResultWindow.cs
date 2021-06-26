using Dalamud.CrystalTower.UI;
using ImGuiNET;
using System.Numerics;

namespace TextToTalk.UI
{
    public class UnlockerResultWindow : ImmediateModeWindow
    {
        public string Text { get; set; }

        public override void Draw(ref bool visible)
        {;
            ImGui.Begin("VoiceUnlocker Result", ref visible, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize);
            {
                ImGui.TextWrapped(Text);
            }
            ImGui.End();
        }
    }
}