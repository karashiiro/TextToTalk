using System;
using Dalamud.Bindings.ImGui;
using System.Net.Http;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Uberduck;

/// <summary>
/// The logic for the Uberduck backend. Uberduck changed its offerings to not include a
/// free option, so this likely won't see many updates going forward.
/// </summary>
public class UberduckBackend : VoiceBackend
{
    private readonly StreamingSoundQueue soundQueue;
    private readonly UberduckBackendUI ui;
    private readonly UberduckClient? uberduck;

    public UberduckBackend(PluginConfiguration config, HttpClient http)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFFDE7312);

        this.soundQueue = new StreamingSoundQueue(config);
        this.uberduck = new UberduckClient(this.soundQueue, http);

        var voices = this.uberduck.UpdateVoices().GetAwaiter().GetResult();
        this.ui = new UberduckBackendUI(config, this.uberduck, () => voices, this);
    }

    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }
    public override void Say(SayRequest request)
    {
        if (request.Voice is not UberduckVoicePreset uberduckVoicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }

        if (this.uberduck == null)
        {
            DetailedLog.Warn("Uberduck client has not yet been initialized");
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await this.uberduck.Say(uberduckVoicePreset.VoiceName, uberduckVoicePreset.PlaybackRate,
                    uberduckVoicePreset.Volume, request.Source, request.Text);
            }
            catch (UberduckFailedException e)
            {
                DetailedLog.Error(e, $"Failed to make Uberduck TTS request ({e.StatusCode}).");
            }
            catch (UberduckMissingCredentialsException e)
            {
                DetailedLog.Warn(e.Message);
            }
            catch (UberduckUnauthorizedException e)
            {
                DetailedLog.Error(e, "Uberduck API keys are incorrect or invalid.");
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
            this.soundQueue.Dispose();
        }
    }
}