using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TextToTalk.Talk;

[StructLayout(LayoutKind.Explicit, Size = 0x280)]
public unsafe struct AddonBattleTalk
{
    [FieldOffset(0x0)] public AtkUnitBase AtkUnitBase;
    [FieldOffset(0x220)] public AtkTextNode* AtkTextNode220;
    [FieldOffset(0x228)] public AtkTextNode* AtkTextNode228;
    [FieldOffset(0x230)] public AtkResNode* AtkResNode230;
    [FieldOffset(0x238)] public AtkNineGridNode* AtkNineGridNode238;
    [FieldOffset(0x240)] public AtkNineGridNode* AtkNineGridNode240;
    [FieldOffset(0x248)] public AtkResNode* AtkResNode248;
    [FieldOffset(0x250)] public AtkImageNode* AtkImageNode250;
    // 0x258:0x260 - Possibly two small i32 values
    // 0x260:0x268 - Pointer to a pointer to a static function - it was just "xor al,al; ret;"
    //               when I looked at it, but it probably gets replaced with something
    //               interesting sometimes
    // 0x268:0x270 - Looks like an enum value of some kind
    [FieldOffset(0x270)] public AtkResNode* AtkResNode270;
    // 0x270:0x278 - Pointer to some sparse-looking object
}