using Dalamud.CrystalTower.Commands.Attributes;
using Dalamud.CrystalTower.UI;
using Dalamud.Plugin;
using TextToTalk.Backends;
using TextToTalk.UI;

namespace TextToTalk.Modules
{
    public class MainCommandModule
    {
        public DalamudPluginInterface PluginInterface { get; set; }
        public PluginConfiguration Config { get; set; }
        public SharedState State { get; set; }
        public WindowManager Windows { get; set; }
        public VoiceBackendManager BackendManager { get; set; }

        [Command("/canceltts")]
        [HelpMessage("Cancel all queued TTS messages.")]
        public void CancelTts(string command, string args)
        {
            BackendManager.CancelSay();
        }

        [Command("/toggletts")]
        [HelpMessage("Toggle TextToTalk's text-to-speech.")]
        public void ToggleTts(string command = "", string args = "")
        {
            if (Config.Enabled)
                DisableTts();
            else
                EnableTts();
        }

        [Command("/disabletts")]
        [HelpMessage("Disable TextToTalk's text-to-speech.")]
        public void DisableTts(string command = "", string args = "")
        {
            Config.Enabled = false;
            BackendManager.CancelSay();
            var chat = PluginInterface.Framework.Gui.Chat;
            chat.Print("TTS disabled.");
            PluginLog.Log("TTS disabled.");
        }

        [Command("/enabletts")]
        [HelpMessage("Enable TextToTalk's text-to-speech.")]
        public void EnableTts(string command = "", string args = "")
        {
            Config.Enabled = true;
            var chat = PluginInterface.Framework.Gui.Chat;
            chat.Print("TTS enabled.");
            PluginLog.Log("TTS enabled.");
        }

        [Command("/tttconfig")]
        [HelpMessage("Toggle TextToTalk's configuration window.")]
        public void ToggleConfig(string command, string args)
        {
            Windows.ToggleWindow<ConfigurationWindow>();
        }
    }
}