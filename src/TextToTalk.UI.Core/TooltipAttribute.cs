using System;

namespace TextToTalk.UI.Core;

[AttributeUsage(AttributeTargets.Property)]
public class TooltipAttribute : Attribute
{
    public string Text { get; }
    public TooltipAttribute(string text) => Text = text;
}