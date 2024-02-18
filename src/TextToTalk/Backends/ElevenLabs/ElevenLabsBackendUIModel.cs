using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using R3;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsBackendUIModel : IDisposable
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly PluginConfiguration config;
    private readonly ReactiveProperty<long> getUserSubscriptionInfoImmediately;
    private readonly IDisposable observeUserSubscriptionInfo;

    private string apiKey;

    /// <summary>
    /// Gets the sound playback queue.
    /// </summary>
    public StreamSoundQueue SoundQueue { get; }

    /// <summary>
    /// Gets the currently-instantiated ElevenLabs client instance.
    /// </summary>
    public ElevenLabsClient ElevenLabs { get; }

    /// <summary>
    /// Gets the exception thrown by the most recent login, or null if the login was successful.
    /// </summary>
    public Exception? ElevenLabsLoginException { get; private set; }

    /// <summary>
    /// Gets the user's latest retrieved subscription info.
    /// </summary>
    public ElevenLabsUserSubscriptionInfo? UserSubscriptionInfo { get; private set; }

    /// <summary>
    /// Gets the valid voices for the current voice engine.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ElevenLabsVoice>> Voices { get; private set; }

    public ElevenLabsBackendUIModel(PluginConfiguration config, HttpClient http)
    {
        SoundQueue = new StreamSoundQueue();
        ElevenLabs = new ElevenLabsClient(SoundQueue, http);
        this.config = config;
        this.getUserSubscriptionInfoImmediately = new ReactiveProperty<long>(0);
        this.observeUserSubscriptionInfo = ObserveUserSubscriptionInfo();
        this.apiKey = "";

        this.Voices = new Dictionary<string, IReadOnlyList<ElevenLabsVoice>>();

        var credentials = ElevenLabsCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            LoginWith(credentials.Password);
        }
    }

    private IDisposable ObserveUserSubscriptionInfo()
    {
        return this.getUserSubscriptionInfoImmediately
            .Debounce(TimeSpan.FromSeconds(3))
            .SelectAwait(async (_, _) =>
            {
                try
                {
                    return await ElevenLabs.GetUserSubscriptionInfo();
                }
                catch (Exception ex)
                {
                    DetailedLog.Error(ex, "Failed to get user subscription info");
                    return null;
                }
            })
            .Where(info => info is not null)
            .SubscribeOnThreadPool()
            .Subscribe(
                info => UserSubscriptionInfo = info,
                ex => DetailedLog.Error(ex, "User subscription update stream has faulted"),
                _ => {});
    }

    /// <summary>
    /// Gets the client's current credentials.
    /// </summary>
    /// <returns>The client's current credentials.</returns>
    public string GetApiKey() => this.apiKey;

    /// <summary>
    /// Dispatches a user subscription info update.
    /// </summary>
    public void UpdateUserSubscriptionInfo()
    {
        this.getUserSubscriptionInfoImmediately.OnNext(0);
    }

    /// <summary>
    /// Logs in with the provided credentials.
    /// </summary>
    /// <param name="testApiKey">The client's API key.</param>
    public void LoginWith(string testApiKey)
    {
        var apiKeyClean = Whitespace.Replace(testApiKey, "");
        if (TryLogin(apiKeyClean))
        {
            // Only save the user's new credentials if the login succeeded
            ElevenLabsCredentialManager.SaveCredentials(apiKeyClean);
            this.apiKey = apiKeyClean;
        }

        UpdateUserSubscriptionInfo();
    }

    /// <summary>
    /// Gets the current voice preset.
    /// </summary>
    /// <returns>The current voice preset, or null if no voice preset is selected.</returns>
    public ElevenLabsVoicePreset? GetCurrentVoicePreset()
        => this.config.GetCurrentVoicePreset<ElevenLabsVoicePreset>();

    /// <summary>
    /// Sets the current voice preset.
    /// </summary>
    /// <param name="id">The preset ID.</param>
    public void SetCurrentVoicePreset(int id)
    {
        this.config.SetCurrentVoicePreset(id);
        this.config.Save();
    }

    private bool TryLogin(string testApiKey)
    {
        ElevenLabsLoginException = null;
        var lastApiKey = ElevenLabs.ApiKey;
        try
        {
            DetailedLog.Info("Testing ElevenLabs authorization status");
            ElevenLabs.ApiKey = testApiKey;
            // This should throw an exception if the API key was incorrect
            var voices = ElevenLabs.GetVoices().GetAwaiter().GetResult();
            Voices = voices
                .Select(kvp =>
                    new KeyValuePair<string, IReadOnlyList<ElevenLabsVoice>>(kvp.Key, kvp.Value.AsReadOnly()))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            DetailedLog.Info("ElevenLabs authorization successful");
            return true;
        }
        catch (Exception e)
        {
            ElevenLabsLoginException = e;
            ElevenLabs.ApiKey = lastApiKey;
            DetailedLog.Error(e, "Failed to initialize ElevenLabs client");
            return false;
        }
    }

    public void Dispose()
    {
        SoundQueue.Dispose();
        this.observeUserSubscriptionInfo.Dispose();
    }
}