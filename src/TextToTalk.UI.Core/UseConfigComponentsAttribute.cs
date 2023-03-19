using System;

namespace TextToTalk.UI.Core;

[AttributeUsage(AttributeTargets.Class)]
public class UseConfigComponentsAttribute : Attribute
{
    public Type Type { get; }

    public UseConfigComponentsAttribute(Type type) => Type = type;
}