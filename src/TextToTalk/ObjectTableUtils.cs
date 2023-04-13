using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using TextToTalk.Talk;

namespace TextToTalk;

public static class ObjectTableUtils
{
    public static GameObject? GetGameObjectByName(ObjectTable objects, SeString? name)
    {
        // Names are complicated; the name SeString can come from chat, meaning it can
        // include the cross-world icon or friend group icons or whatever else.
        if (name is null) return null;
        if (!TalkUtils.TryGetEntityName(name, out var parsedName)) return null;
        if (string.IsNullOrEmpty(name.TextValue)) return null;
        return objects.FirstOrDefault(gObj =>
            TalkUtils.TryGetEntityName(gObj.Name, out var gObjName) && gObjName == parsedName);
    }
}