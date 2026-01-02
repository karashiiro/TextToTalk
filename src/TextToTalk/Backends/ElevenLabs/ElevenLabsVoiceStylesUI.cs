using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Text;
using TextToTalk.UI.Windows;

namespace TextToTalk.Backends.ElevenLabs
{
    public class ElevenLabsVoiceStyles : IVoiceStylesWindow
    {
        private readonly ElevenLabsBackend backend;
        private PluginConfiguration config;
        private bool showVoiceStyles = false;
        private string newStyleBuffer = string.Empty;
        static double lastCopyTime = -1.0;
        static string lastCopiedStyle = "";
        public ElevenLabsVoiceStyles(ElevenLabsBackend backend, PluginConfiguration config)
        {
            this.backend = backend;
            this.config = config;
        }

        public void Draw(IConfigUIDelegates helpers)
        {
            bool shouldAdd = false;
            ImGui.TextDisabled("Elevenlabs V3 allows for custom voice styles.");
            ImGui.TextDisabled("Experiment and have fun!");

            if (ImGui.InputText("##StyleInput", ref newStyleBuffer, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                shouldAdd = true;
            }

            ImGui.SameLine();
            if (ImGui.Button("Add") && !string.IsNullOrWhiteSpace(newStyleBuffer))
            {
                shouldAdd = true;
            }

            if (shouldAdd && !string.IsNullOrWhiteSpace(newStyleBuffer))
            {
                config.CustomVoiceStyles ??= new List<string>();
                config.CustomVoiceStyles.Add(newStyleBuffer);
                config.CustomVoiceStyles.Sort();
                newStyleBuffer = string.Empty;
            }

            ImGui.Separator();

            if (config.CustomVoiceStyles == null || config.CustomVoiceStyles.Count == 0)
            {
                ImGui.TextDisabled("No voice styles have been added yet.");
            }
            else
            {
                for (int i = 0; i < config.CustomVoiceStyles.Count; i++)
                {
                    string style = config.CustomVoiceStyles[i];
                    if (ImGui.Selectable($"{style}##{i}"))
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

                    if (ImGui.BeginPopupContextItem($"context_{i}"))
                    {
                        if (ImGui.MenuItem("Remove Style"))
                        {
                            config.CustomVoiceStyles.RemoveAt(i);
                            ImGui.EndPopup();
                            break;
                        }
                        ImGui.EndPopup();
                    }
                }

            }
        }
    }
}