using System.Collections.Generic;
using TextToTalk.UI.SourceGeneration.Contexts;

namespace TextToTalk.UI.SourceGeneration.EqualityComparers;

public class ToolContextEqualityComparer : IEqualityComparer<ToolContext>
{
    public static readonly ToolContextEqualityComparer Default = new();

    public bool Equals(ToolContext? x, ToolContext? y)
    {
        return x?.AssemblyName == y?.AssemblyName && x?.AssemblyVersion == y?.AssemblyVersion;
    }

    public int GetHashCode(ToolContext obj)
    {
        return obj.GetHashCode();
    }
}