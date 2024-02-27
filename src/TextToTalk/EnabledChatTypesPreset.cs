using System;
using System.Collections.Generic;
using TextToTalk.UI.Core;

namespace TextToTalk;

public class EnabledChatTypesPreset : ISaveable
{
    public int Id { get; set; }

    public bool EnableAllChatTypes { get; set; }

    public IList<int>? EnabledChatTypes { get; set; }

    public string? Name { get; set; }

    public bool UseKeybind { get; set; }

    public VirtualKey.Enum ModifierKey { get; set; }

    public VirtualKey.Enum MajorKey { get; set; }

    private PluginConfiguration? config;

    public EnabledChatTypesPreset(PluginConfiguration config)
    {
        this.config = config;
    }

    public void Initialize(PluginConfiguration pluginConfiguration)
    {
        // When loaded from an existing config file, the constructor is not properly
        // invoked, so we need to support late-initialization here.
        this.config = pluginConfiguration;
    }

    public void Save()
    {
        if (this.config is null)
        {
            throw new InvalidOperationException("No config is attached to this chat types preset.");
        }

        this.config?.Save();
    }
}