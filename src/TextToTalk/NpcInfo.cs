using System;

namespace TextToTalk;

public class NpcInfo : IEquatable<NpcInfo>
{
    public Guid LocalId { get; init; }

    public string Name { get; set; }

    public bool Equals(NpcInfo other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return LocalId.Equals(other.LocalId) && Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == this.GetType() && Equals((NpcInfo)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LocalId);
    }

    public static bool operator ==(NpcInfo left, NpcInfo right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NpcInfo left, NpcInfo right)
    {
        return !Equals(left, right);
    }
}