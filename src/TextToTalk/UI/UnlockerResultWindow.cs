using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace TextToTalk.UI
{
    public class UnlockerResultWindow : Window
    {
        public string Text { get; set; }

        public UnlockerResultWindow() : base("VoiceUnlocker Result", ImGuiWindowFlags.NoResize)
        {
            Size = new Vector2(320, 90);
        }

        public override void Draw()
        {
            ImGui.TextWrapped(Text);
        }
    }
}