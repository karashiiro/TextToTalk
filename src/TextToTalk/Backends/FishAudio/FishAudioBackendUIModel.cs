using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using R3;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioBackendUIModel : IDisposable
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly PluginConfiguration config;
    private readonly ReactiveProperty<long> getApiCreditInfoImmediately;
    private readonly IDisposable observeApiCreditInfo;

    private string apiKey;

    public StreamSoundQueue SoundQueue { get; }

    public FishAudioClient FishAudio { get; }

    public Exception? FishAudioLoginException { get; private set; }

    public FishAudioApiCreditInfo? ApiCreditInfo { get; private set; }

    public IReadOnlyList<FishAudioVoiceModel> VoiceModels { get; private set; }

    public FishAudioBackendUIModel(PluginConfiguration config, HttpClient http)
    {
        SoundQueue = new StreamSoundQueue(config);
        FishAudio = new FishAudioClient(SoundQueue, http);
        this.config = config;
        this.getApiCreditInfoImmediately = new ReactiveProperty<long>(0);
        this.observeApiCreditInfo = ObserveApiCreditInfo();
        this.apiKey = "";

        this.VoiceModels = new List<FishAudioVoiceModel>();

        var credentials = FishAudioCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            LoginWith(credentials.Password);
        }
    }

    private IDisposable ObserveApiCreditInfo()
    {
        return this.getApiCreditInfoImmediately
            .Debounce(TimeSpan.FromSeconds(3))
            .Where(_ => FishAudio.ApiKey is { Length: > 0 })
            .SelectAwait(async (_, _) =>
            {
                try
                {
                    return await FishAudio.GetApiCreditInfo();
                }
                catch (Exception ex)
                {
                    DetailedLog.Error(ex, "Failed to get Fish Audio API credit info");
                    return null;
                }
            })
            .Where(info => info is not null)
            .SubscribeOnThreadPool()
            .Subscribe(
                info => ApiCreditInfo = info,
                ex => DetailedLog.Error(ex, "Fish Audio API credit info stream has faulted"),
                _ => {});
    }

    public string GetApiKey() => this.apiKey;

    public void UpdateApiCreditInfo()
    {
        this.getApiCreditInfoImmediately.OnNext(0);
    }

    public void LoginWith(string testApiKey)
    {
        var apiKeyClean = Whitespace.Replace(testApiKey, "");
        if (TryLogin(apiKeyClean))
        {
            FishAudioCredentialManager.SaveCredentials(apiKeyClean);
            this.apiKey = apiKeyClean;
        }

        UpdateApiCreditInfo();
    }

    public FishAudioVoicePreset? GetCurrentVoicePreset()
        => this.config.GetCurrentVoicePreset<FishAudioVoicePreset>();

    public void SetCurrentVoicePreset(int id)
    {
        this.config.SetCurrentVoicePreset(id);
        this.config.Save();
    }

    private bool TryLogin(string testApiKey)
    {
        FishAudioLoginException = null;
        var lastApiKey = FishAudio.ApiKey;
        try
        {
            DetailedLog.Info("Testing Fish Audio authorization status");
            FishAudio.ApiKey = testApiKey;
            var voiceModels = FishAudio.GetVoiceModels().GetAwaiter().GetResult();
            VoiceModels = voiceModels.ToList();

            DetailedLog.Info("Fish Audio authorization successful");
            return true;
        }
        catch (Exception e)
        {
            FishAudioLoginException = e;
            FishAudio.ApiKey = lastApiKey;
            DetailedLog.Error(e, "Failed to initialize Fish Audio client");
            return false;
        }
    }

    public void Dispose()
    {
        SoundQueue.Dispose();
        this.observeApiCreditInfo.Dispose();
    }
}
