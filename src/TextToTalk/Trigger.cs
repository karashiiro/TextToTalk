using System;
using System.Text.RegularExpressions;
using TextToTalk.UI.Core;

namespace TextToTalk;

public class Trigger : ISaveable
{
    public string Text { get; set; }
    public bool IsRegex { get; set; }
    public bool ShouldRemove { get; set; }

    private readonly PluginConfiguration config;

    public Trigger(PluginConfiguration config)
    {
        this.config = config;

        Text = "";
    }

    public bool Match(string? test)
    {
        if (test is null) return false;
        if (!IsRegex) return test.Contains(Text);

        try
        {
            return Regex.Match(test, Text).Success;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Save()
    {
        this.config.Save();
    }
}