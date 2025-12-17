using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace TextToTalk.UI
{
    public class UnlockerResultWindow : Window
    {
        public string? Text { get; set; }

        public UnlockerResultWindow() : base("VoiceUnlocker Result")
        {
            Size = new Vector2(320, 90);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public override void Draw()
        {
            ImGui.TextWrapped(Text ?? "");
        }
    }
}