using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsBackend : VoiceBackend
{
    private readonly ElevenLabsBackendUI ui;
    private readonly ElevenLabsBackendUIModel uiModel;
    private readonly UiBuilder uiBuilder;

    public ElevenLabsBackend(PluginConfiguration config, HttpClient http, UiBuilder uiBuilder)
    {
        this.uiModel = new ElevenLabsBackendUIModel(config, http);
        this.ui = new ElevenLabsBackendUI(uiModel, config);
        this.uiBuilder = uiBuilder;
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not ElevenLabsVoicePreset elevenLabsVoicePreset)
        {
            throw new InvalidOperationException("Invalid voice preset provided.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await this.uiModel.ElevenLabs.Say(elevenLabsVoicePreset.VoiceId, elevenLabsVoicePreset.PlaybackRate,
                    elevenLabsVoicePreset.Volume, elevenLabsVoicePreset.SimilarityBoost,
                    elevenLabsVoicePreset.Stability, request.Source, request.Text);
                this.uiModel.UpdateUserSubscriptionInfo();
            }
            catch (ElevenLabsUnauthorizedException e)
            {
                DetailedLog.Error(e, "ElevenLabs API key is incorrect or invalid.");
            }
            catch (ElevenLabsFailedException e) when (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                DetailedLog.Error(e, $"Failed to make ElevenLabs TTS request ({e.StatusCode}).");
                this.uiBuilder.AddNotification(
                    "TTS is being rate-limited, please slow down.",
                    title: null,
                    NotificationType.Warning,
                    10000);
            }
            catch (ElevenLabsFailedException e)
            {
                DetailedLog.Error(e, $"Failed to make ElevenLabs TTS request ({e.StatusCode}).");
            }
            catch (ElevenLabsMissingCredentialsException e)
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