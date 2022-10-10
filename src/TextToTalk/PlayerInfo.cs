using System;

namespace TextToTalk;

public class PlayerInfo : IEquatable<PlayerInfo>
{
    public Guid LocalId { get; init; }

    public string Name { get; set; }

    public uint WorldId { get; set; }

    public bool Equals(PlayerInfo other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return LocalId == other.LocalId;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == this.GetType() && Equals((PlayerInfo)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LocalId);
    }

    public static bool operator ==(PlayerInfo left, PlayerInfo right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PlayerInfo left, PlayerInfo right)
    {
        return !Equals(left, right);
    }
}