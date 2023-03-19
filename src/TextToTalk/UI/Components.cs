using System;
using System.Collections.Generic;
using System.Numerics;
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
}