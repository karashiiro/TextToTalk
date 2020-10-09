using System;
using System.Speech.Synthesis;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Plugin;
using TextToTalk.Attributes;

namespace TextToTalk
{
    public class TextToTalk : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<TextToTalk> commandManager;
        private PluginConfiguration config;
        private PluginUI ui;    

        private SpeechSynthesizer speechSynthesizer;

        public string Name => "TextToTalk";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (PluginConfiguration)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.ui = new PluginUI(this.config);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.DrawConfig;
            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => this.ui.ConfigVisible = true;

            this.speechSynthesizer = new SpeechSynthesizer();
            this.pluginInterface.Framework.Gui.Chat.OnChatMessage += OnChatMessage;

            this.commandManager = new PluginCommandManager<TextToTalk>(this, this.pluginInterface);
        }

        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            if (!this.config.Enabled) return;
            if (this.config.DisabledChatTypes.Contains(type)) return;

            PluginLog.Log("Chat message from type {0}: {1}", type, message.TextValue);

            this.speechSynthesizer.Rate = this.config.Rate;
            this.speechSynthesizer.Volume = this.config.Volume;
            this.speechSynthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
            this.speechSynthesizer.SpeakAsync(message.TextValue);
        }

        [Command("/toggletts")]
        [HelpMessage("Toggle TextToTalk's Text-to-Speech.")]
        public void ToggleTts(string command, string args)
        {
            this.config.Enabled = !this.config.Enabled;
            var chat = this.pluginInterface.Framework.Gui.Chat;
            chat.Print($"TTS {(this.config.Enabled ? "enabled" : "disabled")}.");
        }

        [Command("/tttconfig")]
        [HelpMessage("Toggle TextToTalk's configuration window.")]
        public void ToggleConfig(string command, string args)
        {
            this.ui.ConfigVisible = !this.ui.ConfigVisible;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.commandManager.Dispose();

                this.pluginInterface.Framework.Gui.Chat.OnChatMessage -= OnChatMessage;
                this.speechSynthesizer.Dispose();

                this.pluginInterface.SavePluginConfig(this.config);

                this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, args) => this.ui.ConfigVisible = true;
                this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.DrawConfig;

                this.pluginInterface.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
