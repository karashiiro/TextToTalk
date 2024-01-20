using System;
using System.Net.Http;
using ImGuiNET;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackend : VoiceBackend
{
    private StreamSoundQueue soundQueue;
    private readonly PluginConfiguration pluginConfiguration;
    private readonly OpenAiClient client;
    private readonly OpenAiBackendUI ui;
    private readonly OpenAiApiConfig apiConfig;

    public OpenAiBackend(PluginConfiguration pluginConfiguration, HttpClient http)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFF0099FF);

        this.soundQueue = new StreamSoundQueue();
        this.pluginConfiguration = pluginConfiguration;
        this.ui = new OpenAiBackendUI();
        this.apiConfig = new OpenAiApiConfig();
        apiConfig.ApiKey = OpenAiCredentialManager.LoadCredentials()?.Password ?? "";
        this.client = new OpenAiClient(soundQueue, apiConfig, http);
    }
    
    public override void Say(TextSource source, VoicePreset preset, string speaker, string text)
    {
        if (preset is not OpenAiVoicePreset voicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }
        
        _ = client.Say(voicePreset.VoiceName, voicePreset.PlaybackRate, voicePreset.Volume, source, text);
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
        ui.DrawLoginOptions(apiConfig);
        ImGui.Separator();
        ui.DrawVoicePresetOptions(pluginConfiguration);
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return soundQueue.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            soundQueue.Dispose();
        }
    }
}