using System;
using System.Linq.Expressions;
using System.Reflection;
using ImGuiNET;

namespace TextToTalk.UI;

public static class Components
{
    public static IContinuation Toggle<T>(string label, T obj, Expression<Func<T, bool>> propertySelector)
    {
        var continuation = new Continuation();

        var getValue = propertySelector.Compile();
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