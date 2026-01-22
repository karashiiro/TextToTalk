using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using TextToTalk;
using TextToTalk.Backends;
using TextToTalk.UI.Windows;

public class StatsWindow : Window
{
    private float[] dataArray = Array.Empty<float>();
    private DateTime lastUpdateTime = DateTime.MinValue;
    private readonly object updateLock = new();
    public static StatsWindow? Instance { get; private set; }

    private readonly LatencyTracker tracker;
    public bool IsVisible = false;

    public StatsWindow(TextToTalk.LatencyTracker tracker) : base("TTS Statistics")
    {
        this.tracker = tracker;
        Instance = this;
    }

    public void ToggleStats()
    {
        this.IsOpen = !this.IsOpen;
    }

    public override void Draw()
    {
        float[] fullDataArray = tracker.GetHistoryArray();

        ImGui.Text($"Average Latency: {tracker.AverageLatency:F2} ms");
        ImGui.SameLine();
        if (ImGui.Button("Clear History"))
        {
            tracker.Clear();
        }

        if (ImGui.TreeNode("View Raw History"))
        {
            if (ImGui.BeginChild("RawDataList", new System.Numerics.Vector2(0, 150), true))
            {
                for (int i = 0; i < fullDataArray.Length; i++)
                {
                    ImGui.Text($"[{i:000}] {fullDataArray[i]:F2} ms");
                }

                ImGui.EndChild();
            }
            ImGui.TreePop();
        }
    }
}