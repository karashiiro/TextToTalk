using Dalamud.Game.Internal;
using Dalamud.Game.Internal.Gui.Addon;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using TextToTalk.Attributes;

namespace TextToTalk
{
    public class TextToTalk : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<TextToTalk> commandManager;
        private PluginConfiguration config;
        private PluginUI ui;

        private Addon talkAddonInterface;

        private SpeechSynthesizer speechSynthesizer;
        private WsServer wsServer;

        private IntPtr lastTextPtr;

        public string Name => "TextToTalk";

        public void Initialize(DalamudPluginInterface pi)
        {
            this.pluginInterface = pi;

            this.config = (PluginConfiguration)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.wsServer = new WsServer();

            this.ui = new PluginUI(this.config, this.wsServer);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.DrawConfig;
            this.pluginInterface.UiBuilder.OnOpenConfigUi += (_, _) => this.ui.ConfigVisible = true;

            this.speechSynthesizer = new SpeechSynthesizer();
            this.pluginInterface.Framework.Gui.Chat.OnChatMessage += OnChatMessage;

            this.pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;

            this.commandManager = new PluginCommandManager<TextToTalk>(this, this.pluginInterface);
        }

        private unsafe void OnFrameworkUpdate(Framework framework)
        {
            if (this.talkAddonInterface == null || this.talkAddonInterface.Address == IntPtr.Zero)
            {
                this.talkAddonInterface = this.pluginInterface.Framework.Gui.GetAddonByName("Talk", 1);
                return;
            }

            var talkAddon = (AddonTalk*)this.talkAddonInterface.Address.ToPointer();

            // Will be null when there's no Talk window open
            byte* textPtr;
            if (talkAddon->AtkTextNode228 != null)
                textPtr = talkAddon->AtkTextNode228->NodeText.StringPtr;
            else return;

            var managedTextPtr = (IntPtr)textPtr;

            if (managedTextPtr == this.lastTextPtr) return;
            this.lastTextPtr = managedTextPtr;

            var textLength = talkAddon->AtkTextNode228->NodeText.StringLength;
            if (textLength <= 0) return;

            var text = Encoding.UTF8.GetString(textPtr, (int)textLength);
            PluginLog.Log(text); // Replace with TTS if this works
        }

        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            var textValue = message.TextValue;
            if (sender != null && sender.TextValue != string.Empty)
            {
                if (this.config.NameNpcWithSay || (int)type != (int)AdditionalChatTypes.Enum.NPCDialogue)
                {
                    return;
                }
            }

#if DEBUG
            PluginLog.Log("Chat message from type {0}: {1}", type, textValue);
#endif

            if (!this.config.Enabled) return;
            if (this.config.Bad.Where(t => t.Text != "").Any(t => t.Match(textValue))) return;

            var typeAccepted = this.config.EnabledChatTypes.Contains((int)type);
            var goodMatch = this.config.Good
                .Where(t => t.Text != "")
                .Any(t => t.Match(textValue));
            if (!(this.config.EnableAllChatTypes || typeAccepted) || this.config.Good.Count > 0 && !goodMatch) return;

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
