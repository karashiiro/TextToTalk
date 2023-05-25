using System;
using System.Linq;

namespace TextToTalk.Utils;

public static class FunctionalUtils
{
    public static void RunSafely(Action fn, Action<Exception> onFail)
    {
        try
        {
            fn();
        }
        catch (Exception e)
        {
            onFail(e);
        }
    }

    public static T Pipe<T>(T input, params Func<T, T>[] transforms)
    {
        return transforms.Aggregate(input, (agg, next) => next(agg));
    }
}