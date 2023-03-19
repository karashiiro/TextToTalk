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

    private readonly PluginConfiguration config;

    public EnabledChatTypesPreset(PluginConfiguration config)
    {
        this.config = config;
    }

    public void Save()
    {
        this.config.Save();
    }
}