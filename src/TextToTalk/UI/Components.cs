using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;

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

    public static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(text);
            ImGui.EndTooltip();
        }
    }

    public static void HelpTooltip(string text)
    {
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        Tooltip(text);
    }
}