using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TextToTalk.UI;

public static class Components
{
    public static void Table<TRow>(string label, Vector2 size, ImGuiTableFlags flags, Action header,
        Func<IEnumerable<TRow>> rows,
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

    public static void ChooseOutputAudioDevice(string label, PluginConfiguration config)
    {
        var audiodevices = new List<string>();
        foreach (var devname in AudioDevices.deviceList)
        {
            audiodevices.Add(devname.Description);
        }

        var selectedAudioDeviceIndex = config.SelectedAudioDeviceIndex;
        var previewValue = string.Join("\0", audiodevices);
        if (ImGui.Combo("##AudioDevices", ref selectedAudioDeviceIndex, previewValue, audiodevices.Count))
        {
            config.SelectedAudioDeviceIndex = selectedAudioDeviceIndex;
            config.Save();
        }
    }
}