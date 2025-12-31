using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs;
using Lumina.Excel.Sheets;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using TextToTalk.Backends;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.ElevenLabs;
using TextToTalk.Data.Model;
using TextToTalk.GameEnums;
using TextToTalk.Services;
using static TextToTalk.Backends.Azure.AzureClient;

namespace TextToTalk.UI.Windows
{
    public interface IVoiceStylesWindow
    {
        void Draw(IConfigUIDelegates helpers);
    }

    public class AzureVoiceStyles : IVoiceStylesWindow
    {
        private readonly AzureBackend backend;
        private PluginConfiguration config;
        static double lastCopyTime = -1.0;
        static string lastCopiedStyle = "";

        public AzureVoiceStyles(AzureBackend backend, PluginConfiguration config)
        {
            this.backend = backend;
            this.config = config;
            
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
                    ImGui.SetClipboardText($"[{style}]");
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
                config.ElevenLabsVoiceStyles ??= new List<string>();
                config.ElevenLabsVoiceStyles.Add(newStyleBuffer);
                config.ElevenLabsVoiceStyles.Sort();
                newStyleBuffer = string.Empty;
            }

            ImGui.Separator();

            if (config.ElevenLabsVoiceStyles == null || config.ElevenLabsVoiceStyles.Count == 0)
            {
                ImGui.TextDisabled("No voice styles have been added yet.");
            }
            else
            {
                for (int i = 0; i < config.ElevenLabsVoiceStyles.Count; i++)
                {
                    string style = config.ElevenLabsVoiceStyles[i];
                    if (ImGui.Selectable($"{style}##{i}"))
                    {
                        ImGui.SetClipboardText($"[{style}]");
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
                            config.ElevenLabsVoiceStyles.RemoveAt(i);
                            ImGui.EndPopup();
                            break;
                        }
                        ImGui.EndPopup();
                    }
                }

            }
        }
    }
    public class VoiceStyles : Window
    {
        private readonly VoiceBackendManager backendManager;
        private readonly IConfigUIDelegates helpers;
        private readonly PluginConfiguration config;
        private readonly Dictionary<Type, IVoiceStylesWindow> componentCache = new();

        public VoiceStyles(VoiceBackendManager backendManager, IConfigUIDelegates helpers, PluginConfiguration config)
            : base("Voice Styles", ImGuiWindowFlags.None)
        {
            this.backendManager = backendManager;
            this.helpers = helpers;
            this.config = config;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(40, 30),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public override void Draw()
        {
            var activeBackend = backendManager.Backend;
            if (activeBackend == null) return;
            var component = GetOrCreateComponent(activeBackend, config);
            if (component != null)
            {
                component.Draw(helpers);
            }
            else
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey3, "This backend does not yet support dynamic styles.");
            }
        }

        private IVoiceStylesWindow? GetOrCreateComponent(VoiceBackend backend, PluginConfiguration config)
        {
            var type = backend.GetType();
            if (componentCache.TryGetValue(type, out var existing)) return existing;
            IVoiceStylesWindow? newComponent = backend switch
            {
                AzureBackend azure => new AzureVoiceStyles(azure, config),
                ElevenLabsBackend eleven => new ElevenLabsVoiceStyles(eleven, config),
                _ => null
            };

            if (newComponent != null) componentCache[type] = newComponent;
            return newComponent;
        }
    }
}
