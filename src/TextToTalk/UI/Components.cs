﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using TextToTalk.Backends;

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

    public static void ChooseDirectSoundDevice(string label, IPlaybackDeviceProvider playbackDeviceProvider)
    {
        var devices = playbackDeviceProvider.ListDevices();
        var deviceNames = devices.Select(d => d.ModuleName).ToArray();
        var currentDeviceId = playbackDeviceProvider.GetDeviceId();
        var currentIdx = devices.IndexOf(devices.First(d => d.Guid == currentDeviceId));
        if (ImGui.Combo(label, ref currentIdx, deviceNames, devices.Count))
        {
            var newDevice = devices[currentIdx];
            if (newDevice.Guid == currentDeviceId) return;

            playbackDeviceProvider.SetDevice(newDevice);
        }
    }
}