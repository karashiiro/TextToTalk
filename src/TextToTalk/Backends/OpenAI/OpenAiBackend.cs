using Dalamud.Bindings.ImGui;
using OpenAI;
using Serilog;
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
    private readonly LatencyTracker latencyTracker;

    public OpenAiBackend(PluginConfiguration config, HttpClient http, INotificationService notificationService, LatencyTracker latencyTracker)
    {
        this.uiModel = new OpenAiBackendUIModel(config, http, latencyTracker);
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
                await this.uiModel.OpenAi.Say(request.Text, voicePreset.Model, request.Source, voicePreset.VoiceName, !string.IsNullOrWhiteSpace(request.Style) ? request.Style : (voicePreset.Style ?? string.Empty), 1.0f, voicePreset.Volume);
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

        if (uiModel.OpenAi._ttsCts != null)
        {
            uiModel.OpenAi._ttsCts.Cancel();
            uiModel.OpenAi._ttsCts.Dispose();
            uiModel.OpenAi._ttsCts = null;
        }
        this.uiModel.SoundQueue.StopHardware();
        
    }

    public override void CancelSay(TextSource source)
    {
        this.uiModel.SoundQueue.CancelFromSource(source);

        if (uiModel.OpenAi._ttsCts != null)
        {
            uiModel.OpenAi._ttsCts.Cancel();
            uiModel.OpenAi._ttsCts.Dispose();
            uiModel.OpenAi._ttsCts = null;
        }

        if (uiModel.SoundQueue._ttsCts != null)
        {
            uiModel.OpenAi._ttsCts.Cancel();
            uiModel.OpenAi._ttsCts.Dispose();
            uiModel.OpenAi._ttsCts = null;
        }
        this.uiModel.SoundQueue.StopHardware();
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
        if (disposing)
        {
            this.uiModel.SoundQueue.Dispose(); 
        }
    }
}