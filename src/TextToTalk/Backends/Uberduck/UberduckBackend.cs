using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Logging;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends.Uberduck;

public class UberduckBackend : VoiceBackend
{
    private readonly PluginConfiguration config;

    private readonly StreamSoundQueue soundQueue;
    private readonly UberduckBackendUI ui;
    private readonly UberduckClient uberduck;

    private readonly IList<UberduckVoice> voices;

    public UberduckBackend(PluginConfiguration config, HttpClient http)
    {
        TitleBarColor = ImGui.ColorConvertU32ToFloat4(0xFFDE7312);

        this.config = config;

        this.soundQueue = new StreamSoundQueue();
        this.uberduck = new UberduckClient(this.soundQueue, http);

        this.voices = this.uberduck.GetVoices()
            .GetAwaiter().GetResult();
        this.ui = new UberduckBackendUI(this.config, this.uberduck, () => voices);
    }

    public override void Say(TextSource source, VoicePreset voice, string text)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await this.uberduck.Say(voice.VoiceName, this.config.UberduckPlaybackRate, this.config.UberduckVolume, source,
                    text);
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

    public string GetUberduckVoiceForGender(Gender gender)
    {
        var voiceIdStr = this.config.UberduckVoice;
        if (this.config.UseGenderedVoicePresets)
        {
            voiceIdStr = gender switch
            {
                Gender.Male => this.config.UberduckVoiceMale,
                Gender.Female => this.config.UberduckVoiceFemale,
                _ => this.config.UberduckVoiceUngendered,
            };
        }

        // Find the configured voice in the voice list, and fall back to ZWF
        // if it wasn't found in order to avoid a plugin crash.
        var voiceId = this.voices
            .Select(v => v.Name)
            .FirstOrDefault(id => id == voiceIdStr) ?? "zwf";

        return voiceId;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.soundQueue?.Dispose();
        }
    }
}