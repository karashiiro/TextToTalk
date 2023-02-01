using System;
using System.Collections.Generic;
using System.Net.Http;
using Dalamud.Logging;
using ImGuiNET;

namespace TextToTalk.Backends.Azure;

public class AzureBackend : VoiceBackend
{
    private readonly AzureBackendUI ui;

    private AzureClient client;

    public AzureBackend(PluginConfiguration config, HttpClient http)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFFF96800);

        var lexiconManager = new DalamudLexiconManager();
        LexiconUtils.LoadFromConfigAzure(lexiconManager, config);

        IList<string> voices = new List<string>();
        this.ui = new AzureBackendUI(config, lexiconManager, http,
            () => this.client, p => this.client = p, () => voices, v => voices = v);
    }

    public override void Say(TextSource source, VoicePreset preset, string text)
    {
        if (preset is not AzureVoicePreset azureVoicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }

        if (this.client == null)
        {
            PluginLog.LogWarning("Azure client has not yet been initialized");
            return;
        }

        _ = this.client.Say(azureVoicePreset.VoiceName,
            azureVoicePreset.PlaybackRate, azureVoicePreset.Volume, source, text);
    }

    public override void CancelAllSpeech()
    {
        if (this.client == null)
        {
            PluginLog.LogWarning("Azure client has not yet been initialized");
            return;
        }

        _ = this.client.CancelAllSounds();
    }

    public override void CancelSay(TextSource source)
    {
        if (this.client == null)
        {
            PluginLog.LogWarning("Azure client has not yet been initialized");
            return;
        }

        _ = this.client.CancelFromSource(source);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        this.ui.DrawSettings(helpers);
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        if (this.client == null)
        {
            PluginLog.LogWarning("Azure client has not yet been initialized");
            return TextSource.None;
        }

        return this.client.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.client.Dispose();
        }
    }
}