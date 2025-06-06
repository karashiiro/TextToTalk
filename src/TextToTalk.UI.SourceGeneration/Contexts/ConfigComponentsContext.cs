using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TextToTalk.UI.SourceGeneration.Contexts;

public class ConfigComponentsContext : ToolContext
{
    public string? Namespace { get; }

    public string Name { get; }

    public SyntaxTokenList Modifiers { get; }

    public string? ConfigNamespace { get; }

    public string ConfigName { get; }

    public ImmutableArray<Option> ConfigOptions { get; }

    public ConfigComponentsContext(
        string? @namespace,
        string name,
        SyntaxTokenList modifiers,
        string? configNamespace,
        string configName,
        ImmutableArray<Option> configOptions)
    {
        Namespace = @namespace;
        Name = name;
        Modifiers = modifiers;
        ConfigNamespace = configNamespace;
        ConfigName = configName;
        ConfigOptions = configOptions;
    }

    public class Option : IEquatable<Option>
    {
        public string Name { get; }

        public string TypeName { get; }

        public string? Tooltip { get; }

        public Option(string name, string typeName, string? tooltip)
        {
            Name = name;
            TypeName = typeName;
            Tooltip = tooltip;
        }

        public bool Equals(Option? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && TypeName == other.TypeName && Tooltip == other.Tooltip;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((Option)obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() | TypeName.GetHashCode() | (Tooltip?.GetHashCode() ?? 0);
        }
    }
}