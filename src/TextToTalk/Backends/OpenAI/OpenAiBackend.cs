using System;
using System.Net.Http;
using ImGuiNET;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackend : VoiceBackend
{
    private readonly OpenAiClient client;
    private readonly StreamSoundQueue soundQueue;
    private readonly OpenAiBackendUI ui;

    public OpenAiBackend(PluginConfiguration config, HttpClient http)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFF0099FF);

        soundQueue = new StreamSoundQueue();
        client = new OpenAiClient(soundQueue, http);
        ui = new OpenAiBackendUI(config, client);
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not OpenAiVoicePreset voicePreset)
            throw new InvalidOperationException("Invalid voice preset provided.");

        _ = client.Say(voicePreset.VoiceName, voicePreset.PlaybackRate, voicePreset.Volume, request.Source,
            request.Text);
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