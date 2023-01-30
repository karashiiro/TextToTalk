using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using ImGuiNET;

namespace TextToTalk.UI;

public static class Components
{
    public static void Table<TRow>(string label, Vector2 size, ImGuiTableFlags flags, Action header, Func<IEnumerable<TRow>> rows,
        params Action<TRow>[] columns)
    {
        if (ImGui.BeginTable(label, columns.Length, flags, size))
        {
            header();

            foreach (var row in rows())
            {
                ImGui.TableNextRow();
                for (var i = 0; i < columns.Length; i++)
                {
                    var col = columns[i];
                    ImGui.TableSetColumnIndex(i);
                    col(row);
                }
            }

            ImGui.EndTable();
        }
    }

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