using Dalamud.Bindings.ImGui;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TextToTalk.Backends.ElevenLabs;
using TextToTalk.Services;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackend : VoiceBackend
{
    private readonly OpenAiBackendUI ui;
    private readonly OpenAiBackendUIModel uiModel;
    private readonly INotificationService notificationService;

    public OpenAiBackend(PluginConfiguration config, HttpClient http, INotificationService notificationService)
    {
        this.uiModel = new OpenAiBackendUIModel(config, http);
        this.ui = new OpenAiBackendUI(uiModel, config, this);
        this.notificationService = notificationService;
    }

    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not OpenAiVoicePreset voicePreset)
            throw new InvalidOperationException("Invalid voice preset provided.");

        _ = Task.Run(async () =>
        {
            try
            {
                await this.uiModel.OpenAi.Say(voicePreset, request, request.Text, !string.IsNullOrWhiteSpace(request.Style) ? request.Style : (voicePreset.Style ?? string.Empty));
            }
            catch (OpenAiUnauthorizedException e)
            {
                DetailedLog.Error(e, "OpenAI API key is incorrect or invalid.");
            }
            catch (OpenAiFailedException e) when (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                DetailedLog.Error(e, $"Failed to make OpenAI TTS request ({e.StatusCode}).");
                if (e.Error?.Message is { } errorMessage)
                {
                    this.notificationService.NotifyWarning("TTS is being rate-limited.", errorMessage);
                }
                else
                {
                    this.notificationService.NotifyWarning("TTS is being rate-limited.",
                        "Please slow down or adjust your enabled chat channels to reduce load.");
                }
            }
            catch (OpenAiFailedException e)
            {
                DetailedLog.Error(e, $"Failed to make OpenAI TTS request ({e.StatusCode}).");
            }
            catch (OpenAiMissingCredentialsException e)
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
        ui.DrawLoginOptions();
        ImGui.Separator();
        ui.DrawVoicePresetOptions();
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return this.uiModel.SoundQueue.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) this.uiModel.SoundQueue.Dispose();
    }
}