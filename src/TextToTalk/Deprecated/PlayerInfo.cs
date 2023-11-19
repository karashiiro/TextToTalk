// ReSharper disable UnassignedGetOnlyAutoProperty
// ReSharper disable CheckNamespace
namespace TextToTalk;

/// <summary>
/// This class used to represent <see cref="TextToTalk.Data.Model.Player"/>, but now exists only to avoid
/// breaking config upgrades from pre-1.25.0.
/// </summary>
public class PlayerInfo
{
    public string? Name { get; }

    public uint WorldId { get; }

    public string? LocalId { get; }
}