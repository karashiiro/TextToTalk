using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace TextToTalk.CommandModules;

public class CommandModule(ICommandManager commandManager) : IDisposable
{
    private readonly List<string> commandNames = [];

    protected void AddCommand(string name, IReadOnlyCommandInfo.HandlerDelegate method, string help)
    {
        commandManager.AddHandler(name, new CommandInfo(method)
        {
            HelpMessage = help,
            ShowInHelp = true,
        });
        this.commandNames.Add(name);
    }

    protected void RemoveCommand(string name)
    {
        commandManager.RemoveHandler(name);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var commandName in this.commandNames)
            {
                RemoveCommand(commandName);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}