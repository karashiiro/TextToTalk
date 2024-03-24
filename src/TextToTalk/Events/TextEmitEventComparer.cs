using System.Collections.Generic;

namespace TextToTalk.Events;

public class TextEmitEventComparer : EqualityComparer<TextEmitEvent>
{
    private TextEmitEventComparer()
    {
    }

    public static EqualityComparer<TextEmitEvent> Instance { get; } = new TextEmitEventComparer();

    public override bool Equals(TextEmitEvent? x, TextEmitEvent? y)
    {
        return x?.IsEquivalent(y) ?? x == y;
    }

    public override int GetHashCode(TextEmitEvent obj)
    {
        return obj.GetHashCode();
    }
}