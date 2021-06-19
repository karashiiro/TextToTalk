using ImGuiNET;
using System.Diagnostics;
using System.Numerics;

namespace TextToTalk.UI
{
    public class VoiceUnlockerWindow : ImmediateModeWindow
    {
        private const string ManualTutorialText = "Manual tutorial";
        private const string EnableAllText = "Enable all system voices";

        private static readonly Vector4 Red = ImGui.ColorConvertU32ToFloat4(0xFF0000FF);
        private static readonly Vector4 LightRed = ImGui.ColorConvertU32ToFloat4(0xFF8A8AFF);
        private static readonly Vector4 DarkRed = ImGui.ColorConvertU32ToFloat4(0xFF00007D);
        private static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);

        public override void Draw(ref bool visible)
        {
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Red);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, Red);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, LightRed);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, DarkRed);

            ImGui.SetNextWindowSize(new Vector2(480, 320));
            ImGui.Begin("VoiceUnlocker", ref visible, ImGuiWindowFlags.NoResize);
            {
                ImGui.TextWrapped("This modification has only been tested on Windows 10.");
                ImGui.TextWrapped("This function will enable all system TTS voices by " +
                                  "copying keys between sections of your system registry. " +
                                  "This modification may not be automatically undone. If " +
                                  "you would like to perform this modification manually, " +
                                  $"please click the \"{ManualTutorialText}\" button.");
                ImGui.TextWrapped("If you would like to proceed with the automatic " +
                                  $"configuration, please click the \"{EnableAllText}\" " +
                                  "button. System instability related to registry modifications " +
                                  "you have performed previously are not taken into account, " +
                                  "and support will not be provided for those use cases.");

                ImGui.Spacing();

                if (ImGui.Button($"{ManualTutorialText}##VoiceUnlockerButton1"))
                {
                    Process.Start("https://www.reddit.com/r/Windows10/comments/96dx8z/how_unlock_all_windows_10_hidden_tts_voices_for/");
                }

                ImGui.Spacing();

                if (ImGui.Button($"{EnableAllText}##VoiceUnlockerButton2"))
                {
                    var resultWindow = GetWindow<UnlockerResultWindow>();

                    resultWindow.Text = VoiceUnlockerRunner.Execute()
                        ? "Registry modification succeeded. Changes will be applied upon restarting the game."
                        : "VoiceUnlocker failed to start. No registry modifications were made.";

                    visible = false;
                    OpenWindow<UnlockerResultWindow>();
                }

                ImGui.TextColored(HintColor, "Administrative privileges will be requested");
            }
            ImGui.End();

            ImGui.PopStyleColor(4);
        }
    }
}