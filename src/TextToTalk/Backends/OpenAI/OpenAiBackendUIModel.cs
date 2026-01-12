using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.ClientModel;
using OpenAI;
using OpenAI.Models; // Ensure you have the Models namespace

namespace TextToTalk.Backends.OpenAI;

public class OpenAiBackendUIModel
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly PluginConfiguration config;

    private string apiKey;

    /// <summary>
    /// Gets the sound playback queue.
    /// </summary>
    public StreamingSoundQueue SoundQueue { get; }

    //public RawStreamingSoundQueue RawStreamingSoundQueue { get; }

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

    public OpenAiBackendUIModel(PluginConfiguration config, HttpClient http)
    {
        SoundQueue = new StreamingSoundQueue(config);
        var credentials = OpenAiCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            apiKey = (credentials.Password);
        }
        //RawStreamingSoundQueue = new RawStreamingSoundQueue(config);
        OpenAi = new OpenAiClient(SoundQueue, apiKey);
        this.config = config;
        this.apiKey = "";

        // this.Voices = new Dictionary<string, IReadOnlyList<string>>();


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
        DetailedLog.Info("Testing OpenAI authorization status...");

        // 1. Initialize a temporary client with the test key
        // In the v2 SDK, you can use OpenAIModelClient for a cheap validation call
        var modelClient = new OpenAIModelClient(new ApiKeyCredential(testApiKey));

        // 2. Perform a 'List Models' call. 
        // This is a free metadata call that requires valid authentication.
        // Use GetModels() to verify credentials.
        _ = modelClient.GetModels(); 

        // 3. If successful, update the primary ApiKey and return true
        this.apiKey = testApiKey;
        DetailedLog.Info("OpenAI authorization successful.");
        return true;
    }
    catch (ClientResultException e)
    {
        // Specifically catch SDK-based authentication or client errors
        OpenAiLoginException = e;
        this.apiKey = lastApiKey;
        DetailedLog.Error(e, $"OpenAI authorization failed: {e.Status} {e.Message}");
        return false;
    }
    catch (Exception e)
    {
        OpenAiLoginException = e;
        this.apiKey = lastApiKey;
        DetailedLog.Error(e, "An unexpected error occurred during OpenAI initialization.");
        return false;
    }
}

    public void Dispose()
    {
        SoundQueue.Dispose();
    }
}