using System;
using ImGuiNET;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;

namespace TextToTalk.Backends.Uberduck;

public class UberduckBackend : VoiceBackend
{
    private readonly StreamSoundQueue soundQueue;
    private readonly UberduckBackendUI ui;
    private readonly UberduckClient uberduck;

    public UberduckBackend(PluginConfiguration config, HttpClient http)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFFDE7312);

        this.soundQueue = new StreamSoundQueue();
        this.uberduck = new UberduckClient(this.soundQueue, http);

        var voices = this.uberduck.GetVoices().GetAwaiter().GetResult();
        this.ui = new UberduckBackendUI(config, this.uberduck, () => voices);
    }

    public override void Say(TextSource source, VoicePreset preset, string text)
    {
        if (preset is not UberduckVoicePreset uberduckVoicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await this.uberduck.Say(uberduckVoicePreset.VoiceName, uberduckVoicePreset.PlaybackRate,
                    uberduckVoicePreset.Volume, source, text);
            }
            catch (UberduckFailedException e)
            {
                PluginLog.LogError(e, $"Failed to make Uberduck TTS request ({e.StatusCode}).");
            }
            catch (UberduckMissingCredentialsException e)
            {
                PluginLog.LogWarning(e.Message);
            }
            catch (UberduckUnauthorizedException e)
            {
                PluginLog.LogError(e, "Uberduck API keys are incorrect or invalid.");
            }
        });
    }

    public override void CancelAllSpeech()
    {
        this.soundQueue.CancelAllSounds();
    }

    public override void CancelSay(TextSource source)
    {
        this.soundQueue.CancelFromSource(source);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        this.ui.DrawSettings(helpers);
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return this.soundQueue.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.soundQueue?.Dispose();
        }
    }
}