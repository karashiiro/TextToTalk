using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackendUIModel
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly PluginConfiguration config;

    private string apiKey;

    /// <summary>
    /// Gets the sound playback queue.
    /// </summary>
    public StreamSoundQueue SoundQueue { get; }

    /// <summary>
    /// Gets the currently-instantiated OpenAI client instance.
    /// </summary>
    public OpenAiClient OpenAi { get; }

    /// <summary>
    /// Gets the exception thrown by the most recent login, or null if the login was successful.
    /// </summary>
    public Exception? OpenAiLoginException { get; private set; }

    /// <summary>
    /// Gets the valid voices for the current voice engine.
    /// NOTE: Currently there is no endpoint which provides this information for OpenAI.
    /// </summary>
    // public IReadOnlyDictionary<string, IReadOnlyList<string>> Voices { get; private set; }

    public OpenAiBackendUIModel(PluginConfiguration config, IPlaybackDeviceProvider playbackDeviceProvider,
        HttpClient http)
    {
        SoundQueue = new StreamSoundQueue(playbackDeviceProvider);
        OpenAi = new OpenAiClient(SoundQueue, http);
        this.config = config;
        this.apiKey = "";

        // this.Voices = new Dictionary<string, IReadOnlyList<string>>();

        var credentials = OpenAiCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            LoginWith(credentials.Password);
        }
    }

    /// <summary>
    /// Gets the client's current credentials.
    /// </summary>
    /// <returns>The client's current credentials.</returns>
    public string GetApiKey() => this.apiKey;

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
            OpenAiCredentialManager.SaveCredentials(apiKeyClean);
            this.apiKey = apiKeyClean;
        }
    }

    /// <summary>
    /// Gets the current voice preset.
    /// </summary>
    /// <returns>The current voice preset, or null if no voice preset is selected.</returns>
    public OpenAiVoicePreset? GetCurrentVoicePreset()
        => this.config.GetCurrentVoicePreset<OpenAiVoicePreset>();

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
        OpenAiLoginException = null;
        var lastApiKey = this.apiKey;
        try
        {
            DetailedLog.Info("Testing OpenAI authorization status");
            OpenAi.ApiKey = testApiKey;
            // This should throw an exception if the API key was incorrect
            OpenAi.TestCredentials().GetAwaiter().GetResult();
            DetailedLog.Info("OpenAI authorization successful");
            return true;
        }
        catch (Exception e)
        {
            OpenAiLoginException = e;
            OpenAi.ApiKey = lastApiKey;
            DetailedLog.Error(e, "Failed to initialize OpenAI client");
            return false;
        }
    }

    public void Dispose()
    {
        SoundQueue.Dispose();
    }
}