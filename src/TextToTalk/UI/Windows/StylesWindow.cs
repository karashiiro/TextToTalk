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
using TextToTalk.Backends.OpenAI;
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

    public interface IWindowController
    {
        void ToggleStyle();
    }

    public class VoiceStyles : Window
    {
        private readonly VoiceBackendManager backendManager;
        private readonly IConfigUIDelegates helpers;
        private readonly PluginConfiguration config;
        private readonly Dictionary<Type, IVoiceStylesWindow> componentCache = new();
        public static VoiceStyles? Instance { get; private set; }
        public VoiceStyles(VoiceBackendManager backendManager, IConfigUIDelegates helpers, PluginConfiguration config)
            : base("Voice Styles", ImGuiWindowFlags.None)
        {
            Instance = this;

            this.backendManager = backendManager;
            this.helpers = helpers;
            this.config = config;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(40, 30),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void CopyStyleToClipboard(string style)
        {
            ImGui.SetClipboardText($"[[{style}]]");
        }

        public void ToggleStyle()
        {
            this.IsOpen = !this.IsOpen;
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
                AzureBackend azure => new AzureVoiceStyles(azure, config, this),
                ElevenLabsBackend eleven => new ElevenLabsVoiceStyles(eleven, config),
                OpenAiBackend openai => new OpenAIVoiceStyles(openai, config),
                _ => null
            };

            if (newComponent != null) componentCache[type] = newComponent;
            return newComponent;
        }
    }
}
