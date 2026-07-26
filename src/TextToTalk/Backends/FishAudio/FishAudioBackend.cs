using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TextToTalk.Services;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioBackend : VoiceBackend
{
    private readonly FishAudioBackendUI ui;
    private readonly FishAudioBackendUIModel uiModel;
    private readonly INotificationService notificationService;
    private readonly PluginConfiguration config;

    public FishAudioBackend(PluginConfiguration config, HttpClient http, INotificationService notificationService)
    {
        this.uiModel = new FishAudioBackendUIModel(config, http);
        this.ui = new FishAudioBackendUI(uiModel, config, this);
        this.notificationService = notificationService;
        this.config = config;
    }

    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not FishAudioVoicePreset fishAudioVoicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await this.uiModel.FishAudio.Say(fishAudioVoicePreset.VoiceId, fishAudioVoicePreset.PlaybackRate,
                    fishAudioVoicePreset.Volume, fishAudioVoicePreset.Temperature, fishAudioVoicePreset.TopP,
                    fishAudioVoicePreset.ModelId, fishAudioVoicePreset.Latency, request.Source, request.Text);
                this.uiModel.UpdateApiCreditInfo();
            }
            catch (FishAudioUnauthorizedException e)
            {
                DetailedLog.Error(e, "Fish Audio API key is incorrect or invalid.");
            }
            catch (FishAudioFailedException e) when (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                DetailedLog.Error(e, $"Failed to make Fish Audio TTS request ({e.StatusCode}).");
                this.notificationService.NotifyWarning("TTS is being rate-limited.",
                    "Please slow down or adjust your enabled chat channels to reduce load.");
            }
            catch (FishAudioFailedException e)
            {
                DetailedLog.Error(e, $"Failed to make Fish Audio TTS request ({e.StatusCode}).");
            }
            catch (FishAudioMissingCredentialsException e)
            {
                DetailedLog.Warn(e.Message);
            }
        });
    }

    public override void CancelAllSpeech()
    {
        this.uiModel.SoundQueue.CancelAllSounds();
    }

    public override void CancelSay(TextSource source)
    {
        this.uiModel.SoundQueue.CancelFromSource(source);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        this.ui.DrawSettings();
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return this.uiModel.SoundQueue.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.uiModel.Dispose();
        }
    }
}
