using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace TextToTalk.CommandModules;

public class DebugCommandModule : CommandModule
{
    private readonly IChatGui chat;
    private readonly IGameGui gui;
    private readonly IFramework framework;

    public DebugCommandModule(ICommandManager commandManager, IChatGui chat, IGameGui gui, IFramework framework) :
        base(commandManager)
    {
        this.chat = chat;
        this.gui = gui;
        this.framework = framework;

        RegisterCommands();
    }

    [Conditional("DEBUG")]
    private void RegisterCommands()
    {
        AddCommand("/showbattletalk", ShowBattleTalk, "");
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
}