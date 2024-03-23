using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using TextToTalk.Backends;
using TextToTalk.UI;

// ReSharper disable UnusedMember.Global

namespace TextToTalk.CommandModules
{
    public class MainCommandModule : IDisposable
    {
        private readonly IChatGui chat;
        private readonly IGameGui gui;
        private readonly ICommandManager commandManager;
        private readonly IFramework framework;

        private readonly PluginConfiguration config;
        private readonly VoiceBackendManager backendManager;
        private readonly ConfigurationWindow configurationWindow;

        private readonly IList<string> commandNames;

        public MainCommandModule(IChatGui chat, ICommandManager commandManager, PluginConfiguration config,
            VoiceBackendManager backendManager, ConfigurationWindow configurationWindow, IGameGui gui,
            IFramework framework)
        {
            this.chat = chat;
            this.gui = gui;
            this.commandManager = commandManager;
            this.framework = framework;

            this.config = config;
            this.backendManager = backendManager;
            this.configurationWindow = configurationWindow;

            this.commandNames = new List<string>();

#if DEBUG
            AddCommand("/showbattletalk", ShowBattleTalk, "");
#endif
            AddCommand("/canceltts", CancelTts, "Cancel all queued TTS messages.");
            AddCommand("/toggletts", ToggleTts, "Toggle TextToTalk's text-to-speech.");
            AddCommand("/disabletts", DisableTts, "Disable TextToTalk's text-to-speech.");
            AddCommand("/enabletts", EnableTts, "Enable TextToTalk's text-to-speech.");
            AddCommand("/tttconfig", ToggleConfig, "Toggle TextToTalk's configuration window.");
        }

        public void ShowBattleTalk(string command = "", string args = "")
        {
            const string name = "Test";
            const string message = "Test Text";

            _ = Task.Run(async () =>
            {
                await this.framework.RunOnFrameworkThread(() =>
                {
                    this.chat.Print(new XivChatEntry
                    {
                        Name = name,
                        Message = message,
                        Type = XivChatType.NPCDialogueAnnouncements,
                    });
                });
                await Task.Delay(1000);
                await this.framework.RunOnFrameworkThread(() =>
                {
                    unsafe
                    {
                        var ui = (UIModule*)this.gui.GetUIModule();
                        ui->ShowBattleTalk(name, message, 60f, 0);
                    }
                });
            });
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

        private void AddCommand(string name, CommandInfo.HandlerDelegate method, string help)
        {
            this.commandManager.AddHandler(name, new CommandInfo(method)
            {
                HelpMessage = help,
                ShowInHelp = true,
            });
            this.commandNames.Add(name);
        }

        private void RemoveCommand(string name)
        {
            this.commandManager.RemoveHandler(name);
        }

        public void Dispose()
        {
            foreach (var commandName in this.commandNames)
            {
                RemoveCommand(commandName);
            }
        }
    }
}