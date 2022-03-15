using System.Numerics;
using Dalamud.CrystalTower.UI;
using ImGuiNET;

namespace TextToTalk.UI.Dalamud
{
    public class UnlockerResultWindow : ImmediateModeWindow
    {
        public string Text { get; set; }

        public override void Draw(ref bool visible)
        {
            ImGui.SetNextWindowSize(new Vector2(320, 90));
            ImGui.Begin("VoiceUnlocker Result", ref visible, ImGuiWindowFlags.NoResize);
            {
                ImGui.TextWrapped(Text);
            }
            ImGui.End();
        }
    }
}