using System;
using Dalamud.Bindings.ImGui;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudBackend : VoiceBackend
{
    private readonly GoogleCloudClient client;
    private readonly StreamingSoundQueue soundQueue;
    private readonly GoogleCloudBackendUI ui;
    private readonly LatencyTracker latencyTracker;

    public GoogleCloudBackend(PluginConfiguration config, LatencyTracker latencyTracker)
    {
        soundQueue = new StreamingSoundQueue(config, latencyTracker);
        client = new GoogleCloudClient(soundQueue, config.GoogleCreds);
        ui = new GoogleCloudBackendUI(config, client, this);
    }

    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }
    public override void Say(SayRequest request)
    {
        if (request.Voice is not GoogleCloudVoicePreset voicePreset)
            throw new InvalidOperationException("Invalid voice preset provided.");

        _ = client.Say(voicePreset.Locale, voicePreset.VoiceName, voicePreset.PlaybackRate, voicePreset.Volume, request.Source,
            request.Text);
    }

    public override void CancelAllSpeech()
    {
        soundQueue.CancelAllSounds();
        if (client._TtsCts != null)
        {
            client._TtsCts?.Cancel();
        }
        soundQueue.StopHardware();
    }

    public override void CancelSay(TextSource source)
    {
        soundQueue.CancelFromSource(source);
        if (client._TtsCts != null)
        {
            client._TtsCts?.Cancel();
        }
        soundQueue.StopHardware();
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        ui.DrawLoginOptions();
        ImGui.Separator();
        ui.DrawVoicePresetOptions();
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return soundQueue.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) soundQueue.Dispose();
    }
}