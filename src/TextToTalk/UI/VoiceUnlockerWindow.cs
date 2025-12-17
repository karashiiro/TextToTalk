using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using R3;

namespace TextToTalk.UI
{
    public class VoiceUnlockerWindow : Window, IDisposable
    {
        private const string ManualTutorialText = "Manual tutorial";
        private const string EnableAllText = "Enable all system voices";

        private readonly Subject<string> onResult;
        private readonly VoiceUnlockerRunner voiceUnlockerRunner;

        public VoiceUnlockerWindow(VoiceUnlockerRunner voiceUnlockerRunner) : base("VoiceUnlocker")
        {
            this.onResult = new Subject<string>();
            this.voiceUnlockerRunner = voiceUnlockerRunner;

            Size = new Vector2(480, 320);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public Observable<string> OnResult()
        {
            return this.onResult;
        }

        public override void PreDraw()
        {
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ImColor.Red);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, ImColor.Red);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImColor.LightRed);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImColor.DarkRed);
        }

        public override void Draw()
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

            if (ImGui.Button($"{ManualTutorialText}##{MemoizedId.Create()}"))
            {
                WebBrowser.Open(
                    "https://www.reddit.com/r/Windows10/comments/96dx8z/how_unlock_all_windows_10_hidden_tts_voices_for/");
            }

            ImGui.Spacing();

            if (ImGui.Button($"{EnableAllText}##{MemoizedId.Create()}"))
            {
                var resultText = this.voiceUnlockerRunner.Execute()
                    ? "Registry modification succeeded. Changes will be applied upon restarting the game."
                    : "VoiceUnlocker failed to start. No registry modifications were made.";

                IsOpen = false;
                this.onResult.OnNext(resultText);
            }

            ImGui.TextColored(ImColor.HintColor, "Administrative privileges will be requested");
        }

        public override void PostDraw()
        {
            ImGui.PopStyleColor(4);
        }

        public void Dispose()
        {
            onResult.Dispose();
        }
    }
}