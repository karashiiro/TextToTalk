using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;

namespace TextToTalk;

public static class ObjectTableUtils
{
    public static GameObject? GetGameObjectByName(ObjectTable objects, string? name)
    {
        return !string.IsNullOrEmpty(name)
            ? objects.FirstOrDefault(gObj => gObj.Name.TextValue == name)
            : null;
    }
}