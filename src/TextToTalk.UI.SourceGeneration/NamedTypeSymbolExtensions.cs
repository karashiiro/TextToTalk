using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace TextToTalk.UI.SourceGeneration;

internal static class NamedTypeSymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetThisAndSubtypes(this INamedTypeSymbol? type)
    {
        var current = type;
        while (current is not null)
        {
            yield return current;
            current = current.BaseType;
        }
    }
}