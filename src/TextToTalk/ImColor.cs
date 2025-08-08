using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace TextToTalk;

public class ImColor
{
    public static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
    public static readonly Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
    public static readonly Vector4 Red = new(1, 0, 0, 1);
    public static readonly Vector4 LightRed = ImGui.ColorConvertU32ToFloat4(0xFF8A8AFF);
    public static readonly Vector4 DarkRed = ImGui.ColorConvertU32ToFloat4(0xFF00007D);
}