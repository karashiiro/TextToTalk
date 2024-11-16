using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using TextToTalk.GameEnums;
using TextToTalk.UngenderedOverrides;

namespace TextToTalk.Utils;

public static class CharacterGenderUtils
{
    // TODO: Use NPC ID instead of reading the model information :(
    public static unsafe Gender GetCharacterGender(IGameObject? gObj, UngenderedOverrideManager overrides)
    {
        if (gObj == null || gObj.Address == nint.Zero)
        {
            DetailedLog.Info("GameObject is null; cannot check gender.");
            return Gender.None;
        }

        var charaStruct = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)gObj.Address;

        // Get actor gender as defined by its struct.
        var actorGender = (Gender)charaStruct->DrawData.CustomizeData.Sex;

        // Player gender overrides will be handled by a different system.
        if (gObj.ObjectKind is ObjectKind.Player)
        {
            return actorGender;
        }

        // Get the actor's model ID to see if we have an ungendered override for it.
        // Actors only have 0/1 genders regardless of their canonical genders, so this
        // needs to be specified by us. If an actor is canonically ungendered, their
        // gender seems to always be left at 0 (male).
        var modelId = charaStruct->ModelCharaId;

        // Get the override state and log the model ID so that we can add it to our overrides file if needed.
        if (overrides.IsUngendered(modelId))
        {
            actorGender = Gender.None;
            DetailedLog.Info(
                $"Got model ID {modelId} for {gObj.ObjectKind} \"{gObj.Name}\" (gender overriden to: {actorGender})");
        }
        else
        {
            DetailedLog.Info(
                $"Got model ID {modelId} for {gObj.ObjectKind} \"{gObj.Name}\" (gender read as: {actorGender})");
        }

        return actorGender;
    }
}