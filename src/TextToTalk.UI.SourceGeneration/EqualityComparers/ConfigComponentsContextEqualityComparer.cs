using System.Collections.Generic;
using System.Linq;
using TextToTalk.UI.SourceGeneration.Contexts;

namespace TextToTalk.UI.SourceGeneration.EqualityComparers;

public class ConfigComponentsContextEqualityComparer : IEqualityComparer<ConfigComponentsContext>
{
    public static readonly ConfigComponentsContextEqualityComparer Default = new();

    public bool Equals(ConfigComponentsContext? x, ConfigComponentsContext? y)
    {
        if (x is not null && y is not null)
        {
            if (!x.ConfigOptions.SequenceEqual(y.ConfigOptions))
            {
                return false;
            }
        }

        return ToolContextEqualityComparer.Default.Equals(x, y) &&
               x?.Namespace == y?.Namespace &&
               x?.Name == y?.Name &&
               string.Join(' ', x?.Modifiers) == string.Join(' ', y?.Modifiers) &&
               x?.ConfigNamespace == y?.ConfigNamespace &&
               x?.ConfigName == y?.ConfigName;
    }

    public int GetHashCode(ConfigComponentsContext obj)
    {
        return obj.GetHashCode();
    }
}