using System;

namespace TextToTalk.Middleware;

public class ComponentUpdateState<T> where T : struct, IEquatable<T>
{
    public Action<T> OnUpdate { get; set; }

    private T lastValue;

    public ComponentUpdateState()
    {
        OnUpdate = _ => { };
    }

    public void Mutate(T nextValue)
    {
        if (this.lastValue.Equals(nextValue))
        {
            return;
        }

        this.lastValue = nextValue;
        OnUpdate(nextValue);
    }
}
