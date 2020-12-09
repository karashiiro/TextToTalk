using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Speech.Synthesis;
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
        private WsServer wsServer;

        public string Name => "TextToTalk";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (PluginConfiguration)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.wsServer = new WsServer();

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
            if (sender != null && sender.TextValue != string.Empty)
            {
                textValue = $"{sender.TextValue} says {textValue}";
            }

#if DEBUG
            PluginLog.Log("Chat message from type {0}: {1}", type, textValue);
#endif

            if (!this.config.Enabled) return;
            if (this.config.Bad.Where(t => t.Text != "").Any(t => t.Match(textValue))) return;

            var typeAccepted = this.config.EnableAllChatTypes || this.config.EnabledChatTypes.Contains((int)type);
            var goodMatch = this.config.Good.Count > 0 && this.config.Good
                .Where(t => t.Text != "")
                .Any(t => t.Match(textValue));
            if (!typeAccepted || !goodMatch) return;

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

                if (this.speechSynthesizer.Voice.Name != this.config.VoiceName)
                {
                    this.speechSynthesizer.SelectVoice(this.config.VoiceName);
                }

                this.speechSynthesizer.SpeakAsync(textValue);
            }
        }

        [Command("/canceltts")]
        [HelpMessage("Cancel all queued TTS messages.")]
        public void CancelTts(string command, string args)
        {
            if (this.config.UseWebsocket)
            {
                this.wsServer.Cancel();
#if DEBUG
                PluginLog.Log("Canceled TTS over WebSocket server.");
#endif
            }
            else
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
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
