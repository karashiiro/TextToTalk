using Dalamud.Bindings.ImGui;
using System.Linq;
using TextToTalk;
using TextToTalk.Backends;
using TextToTalk.Backends.Azure;
using TextToTalk.UI.Windows;
using static TextToTalk.Backends.Azure.AzureClient;

public class AzureVoiceStyles : IVoiceStylesWindow
{
    private readonly AzureBackend backend;
    private PluginConfiguration config;
    private VoiceStyles voiceStyles;
    static double lastCopyTime = -1.0;
    static string lastCopiedStyle = "";

    public AzureVoiceStyles(AzureBackend backend, PluginConfiguration config, VoiceStyles voiceStyles)
    {
        this.backend = backend;
        this.config = config;
        this.voiceStyles = voiceStyles;

    }

    public void Draw(IConfigUIDelegates helpers)
    {
        var currentVoicePreset = this.config.GetCurrentVoicePreset<AzureVoicePreset>();
        var voiceDetails = this.backend.voices
            .OrderBy(v => v.Name)
            .FirstOrDefault(v => v?.Name == currentVoicePreset?.VoiceName);

        if (voiceDetails?.Styles == null || voiceDetails.Styles.Count == 0)
        {
            ImGui.TextDisabled("No styles available for this voice.");
            return;
        }

        ImGui.Text("Click a style to copy its tag to clipboard:");
        ImGui.Separator();

        foreach (var style in voiceDetails.Styles)
        {
            if (string.IsNullOrEmpty(style)) continue;

            if (ImGui.Selectable(style))
            {
                VoiceStyles.Instance?.CopyStyleToClipboard(style);
                lastCopyTime = ImGui.GetTime();
                lastCopiedStyle = style;
            }

            if (lastCopiedStyle == style && (ImGui.GetTime() - lastCopyTime < 1.0))
            {
                ImGui.SetTooltip("Copied!");
            }
            else if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Click to copy");
            }

        }
    }
}