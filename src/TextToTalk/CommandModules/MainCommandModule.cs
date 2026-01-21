using Dalamud.Plugin.Services;
using TextToTalk.Backends;
using TextToTalk.UI;
using TextToTalk.UI.Windows;

namespace TextToTalk.CommandModules;

public class MainCommandModule : CommandModule
{
    private readonly IChatGui chat;

    private readonly PluginConfiguration config;
    private readonly VoiceBackendManager backendManager;
    private readonly ConfigurationWindow configurationWindow;
    private readonly IConfigUIDelegates configUIDelegates;
    private readonly VoiceStyles StylesWindow;
    private readonly StatsWindow StatsWindow;

    public MainCommandModule(ICommandManager commandManager, IChatGui chat, PluginConfiguration config,
        VoiceBackendManager backendManager, ConfigurationWindow configurationWindow, IConfigUIDelegates configUIDelegates, VoiceStyles StylesWindow, StatsWindow statsWindow) : base(commandManager) //ElevenLabsStylesWindow elevenLabsStylesWindow)
    {
        this.chat = chat;

        this.config = config;
        this.backendManager = backendManager;
        this.configurationWindow = configurationWindow;
        this.configUIDelegates = configUIDelegates;
        this.StylesWindow = StylesWindow;
        this.StatsWindow = statsWindow;

        AddCommand("/canceltts", CancelTts, "Cancel all queued TTS messages.");
        AddCommand("/toggletts", ToggleTts, "Toggle TextToTalk's text-to-speech.");
        AddCommand("/disabletts", DisableTts, "Disable TextToTalk's text-to-speech.");
        AddCommand("/enabletts", EnableTts, "Enable TextToTalk's text-to-speech.");
        AddCommand("/tttconfig", ToggleConfig, "Toggle TextToTalk's configuration window.");
        AddCommand("/tttstyles", ToggleStyles, "Toggle TextToTalk's styles window.");
        AddCommand("/tttstats", ToggleStats, "Toggle TextToTalk's latency stats window.");
        
    }

    public void CancelTts(string command = "", string args = "")
    {
        this.backendManager.CancelAllSpeech();
    }

    public void ToggleTts(string command = "", string args = "")
    {
        if (this.config.Enabled)
            DisableTts();
        else
            EnableTts();
    }

    public void DisableTts(string command = "", string args = "")
    {
        this.config.Enabled = false;
        CancelTts();
        this.chat.Print("TTS disabled.");
        DetailedLog.Info("TTS disabled.");
    }

    public void EnableTts(string command = "", string args = "")
    {
        this.config.Enabled = true;
        this.chat.Print("TTS enabled.");
        DetailedLog.Info("TTS enabled.");
    }

    public void ToggleConfig(string command = "", string args = "")
    {
        this.configurationWindow.Toggle();
    }
    public void ToggleStyles(string command = "", string args = "")
    {
        this.StylesWindow.Toggle();
    }

    public void ToggleStats(string command = "", string args = "")
    {
        this.StatsWindow.Toggle();
    }
}