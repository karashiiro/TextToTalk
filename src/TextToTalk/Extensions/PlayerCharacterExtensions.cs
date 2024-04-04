using Dalamud.Game.ClientState.Objects.SubKinds;

namespace TextToTalk.Extensions;

public static class PlayerCharacterExtensions
{
    public static string? GetFirstName(this PlayerCharacter playerCharacter)
    {
        var parts = playerCharacter.GetFullName().Split(" ");
        return parts is [{ } firstName, not null] ? firstName : null;
    }

    public static string? GetLastName(this PlayerCharacter playerCharacter)
    {
        var parts = playerCharacter.GetFullName().Split(" ");
        return parts is [not null, { } lastName] ? lastName : null;
    }

    public static string GetFullName(this PlayerCharacter playerCharacter)
    {
        return playerCharacter.Name.TextValue;
    }
}