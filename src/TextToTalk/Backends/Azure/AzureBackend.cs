using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Net.Http;
using static TextToTalk.Backends.Azure.AzureClient;

namespace TextToTalk.Backends.Azure;

public class AzureBackend : VoiceBackend
{
    private readonly AzureBackendUI ui;
    private readonly AzureBackendUIModel uiModel;
    public List<VoiceDetails> voices;
    private readonly LatencyTracker latencyTracker;

    public AzureBackend(PluginConfiguration config, HttpClient http, LatencyTracker latencyTracker)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFFF96800);

        var lexiconManager = new DalamudLexiconManager();
        LexiconUtils.LoadFromConfigAzure(lexiconManager, config);

        this.uiModel = new AzureBackendUIModel(config, lexiconManager, latencyTracker);
        this.voices = this.uiModel.voices;
        this.ui = new AzureBackendUI(this.uiModel, config, lexiconManager, http, this);
        this.latencyTracker = latencyTracker;
    }

    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not AzureVoicePreset azureVoicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }

        if (this.uiModel.Azure == null)
        {
            DetailedLog.Warn("Azure client has not yet been initialized");
            return;
        }

        _ = this.uiModel.Azure.Say(azureVoicePreset.VoiceName,
            azureVoicePreset.PlaybackRate, azureVoicePreset.Volume, request.Source, request.Text, !string.IsNullOrWhiteSpace(request.Style) ? request.Style : (azureVoicePreset.Style ?? string.Empty));
    }

    public override void CancelAllSpeech()
    {
        if (this.uiModel.Azure == null)
        {
            DetailedLog.Warn("Azure client has not yet been initialized");
            return;
        }

        _ = this.uiModel.Azure.CancelAllSounds();
    }

    public override void CancelSay(TextSource source)
    {
        if (this.uiModel.Azure == null)
        {
            DetailedLog.Warn("Azure client has not yet been initialized");
            return;
        }

        _ = this.uiModel.Azure.CancelFromSource(source);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        this.ui.DrawSettings(helpers);
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        if (this.uiModel.Azure == null)
        {
            DetailedLog.Warn("Azure client has not yet been initialized");
            return TextSource.None;
        }

        return this.uiModel.Azure.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.uiModel.Azure?.Dispose();
        }
    }

}