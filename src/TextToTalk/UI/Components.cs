using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using ImGuiNET;

namespace TextToTalk.UI;

public static class Components
{
    private static readonly Dictionary<object, object> CachedExpressions = new();

    private static Func<T, bool> FetchOrCompileExpression<T>(Expression<Func<T, bool>> expr)
    {
        if (CachedExpressions.TryGetValue(expr, out var fn))
        {
            return (Func<T, bool>)fn;
        }

        var nextFn = expr.Compile();
        CachedExpressions[expr] = nextFn;
        return nextFn;
    }

    public static IContinuation Toggle<T>(string label, T obj, Expression<Func<T, bool>> propertySelector)
    {
        var continuation = new Continuation();

        var getValue = FetchOrCompileExpression(propertySelector);
        var value = getValue(obj);
        if (ImGui.Checkbox(label, ref value))
        {
            var prop = (PropertyInfo)((MemberExpression)propertySelector.Body).Member;
            prop.SetValue(obj, value);
            continuation.Invoke();
        }

        return continuation;
    }

    public interface IContinuation
    {
        public IContinuation AndThen(Action action);
    }

    public struct Continuation : IContinuation
    {
        private Action andThen;

        public Continuation()
        {
            this.andThen = () => { };
        }

        public IContinuation AndThen(Action action)
        {
            this.andThen += action;
            return this;
        }

        public void Invoke()
        {
            this.andThen.Invoke();
        }
    }
}