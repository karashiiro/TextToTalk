using System;
using ImGuiNET;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudBackend : VoiceBackend
{
    private readonly GoogleCloudClient client;
    private readonly StreamSoundQueue soundQueue;
    private readonly GoogleCloudBackendUI ui;

    public GoogleCloudBackend(PluginConfiguration config, IPlaybackDeviceProvider playbackDeviceProvider)
    {
        soundQueue = new StreamSoundQueue(playbackDeviceProvider);
        client = new GoogleCloudClient(soundQueue, config.GoogleCreds);
        ui = new GoogleCloudBackendUI(config, client);
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not GoogleCloudVoicePreset voicePreset)
            throw new InvalidOperationException("Invalid voice preset provided.");

        _ = client.Say(voicePreset.Locale, voicePreset.VoiceName, voicePreset.SampleRate, voicePreset.Pitch,
            voicePreset.PlaybackRate, voicePreset.Volume, request.Source, request.Text);
    }

    public override void CancelAllSpeech()
    {
        soundQueue.CancelAllSounds();
    }

    public override void CancelSay(TextSource source)
    {
        soundQueue.CancelFromSource(source);
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