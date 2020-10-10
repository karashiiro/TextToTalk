using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
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
        private WSServer wsServer;

        public string Name => "TextToTalk";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (PluginConfiguration)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.wsServer = new WSServer();

            this.ui = new PluginUI(this.config, this.wsServer);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.DrawConfig;
            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => this.ui.ConfigVisible = true;

            this.speechSynthesizer = new SpeechSynthesizer();
            this.pluginInterface.Framework.Gui.Chat.OnChatMessage += OnChatMessage;

            this.commandManager = new PluginCommandManager<TextToTalk>(this, this.pluginInterface);
        }

        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            var textValue = message.TextValue;

#if DEBUG
            PluginLog.Log("Chat message from type {0}: {1}", type, textValue);
#endif

            if (!this.config.Enabled) return;
            if (this.config.Bad.Count > 0 && this.config.Bad.Where(t => t.Text != "").Any(t => t.Match(textValue))) return;

            var typeAccepted = this.config.EnabledChatTypes.Contains((int)type);
            var goodMatch = this.config.Good.Count > 0 && this.config.Good
                .Where(t => t.Text != "")
                .Any(t => t.Match(textValue));
            if (!(this.config.EnableAllChatTypes || typeAccepted) || !goodMatch) return;

            if (this.config.UseWebsocket && !this.wsServer.Active)
            {
                this.wsServer.Start();
            }
            else if (!this.config.UseWebsocket && this.wsServer.Active)
            {
                this.wsServer.Stop();
            }

            if (this.config.UseWebsocket)
            {
                this.wsServer.Broadcast(textValue);
#if DEBUG
                PluginLog.Log("Sent message {0} on WebSocket server.", textValue);
#endif
            }
            else
            {
                this.speechSynthesizer.Rate = this.config.Rate;
                this.speechSynthesizer.Volume = this.config.Volume;
                this.speechSynthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                this.speechSynthesizer.SpeakAsync(textValue);
            }
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
            if (!disposing) return;

            this.commandManager.Dispose();

            this.pluginInterface.Framework.Gui.Chat.OnChatMessage -= OnChatMessage;
            this.speechSynthesizer.Dispose();

            this.wsServer.Stop();

            this.pluginInterface.SavePluginConfig(this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, args) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.DrawConfig;

            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
