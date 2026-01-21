using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TextToTalk.Lexicons;
using static TextToTalk.Backends.Azure.AzureClient;

namespace TextToTalk.Backends.Azure;

public class AzureBackendUIModel
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly PluginConfiguration config;
    private readonly LexiconManager lexiconManager;

    public List<VoiceDetails> voices;
    private AzureLoginInfo loginInfo;

    private readonly LatencyTracker latencyTracker;

    /// <summary>
    /// Gets the currently-instantiated Azure client instance.
    /// </summary>
    public AzureClient? Azure { get; private set; }

    /// <summary>
    /// Gets the exception thrown by the most recent login, or null if the login was successful.
    /// </summary>
    public Exception? AzureLoginException { get; private set; }
    
    /// <summary>
    /// Gets the available voices.
    /// </summary>
    public IReadOnlyList<VoiceDetails> Voices => this.voices;

    public AzureBackendUIModel(PluginConfiguration config, LexiconManager lexiconManager, LatencyTracker latencyTracker)
    {
        this.config = config;
        this.lexiconManager = lexiconManager;
        this.voices = new List<VoiceDetails>();

        this.loginInfo = new AzureLoginInfo();
        this.latencyTracker = latencyTracker;
        var credentials = AzureCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.loginInfo.Region = credentials.UserName;
            this.loginInfo.SubscriptionKey = credentials.Password;

            TryAzureLogin();
        }
    }
    
    /// <summary>
    /// Gets the client's current credentials.
    /// </summary>
    /// <returns>The client's current credentials.</returns>
    public AzureLoginInfo GetLoginInfo()
        => this.loginInfo;

    /// <summary>
    /// Logs in with the provided credentials.
    /// </summary>
    /// <param name="region">The client's region.</param>
    /// <param name="subscriptionKey">The client's subscription key.</param>
    public void LoginWith(string region, string subscriptionKey)
    {
        var username = Whitespace.Replace(region, "");
        var password = Whitespace.Replace(subscriptionKey, "");
        this.loginInfo = new AzureLoginInfo { Region = username, SubscriptionKey = password };

        if (TryAzureLogin())
        {
            // Only save the user's new credentials if the login succeeded
            AzureCredentialManager.SaveCredentials(username, password);
        }
    }
    
    /// <summary>
    /// Gets the current voice preset.
    /// </summary>
    /// <returns>The current voice preset, or null if no voice preset is selected.</returns>
    public AzureVoicePreset? GetCurrentVoicePreset()
        => this.config.GetCurrentVoicePreset<AzureVoicePreset>();

    /// <summary>
    /// Sets the current voice preset.
    /// </summary>
    /// <param name="id">The preset ID.</param>
    public void SetCurrentVoicePreset(int id)
    {
        this.config.SetCurrentVoicePreset(id);
        this.config.Save();
    }

    private bool TryAzureLogin()
    {
        AzureLoginException = null;
        Azure?.Dispose();
        try
        {
            DetailedLog.Info($"Logging into Azure region {this.loginInfo.Region}");
            Azure = new AzureClient(this.loginInfo.SubscriptionKey, this.loginInfo.Region, this.lexiconManager, this.config, this.latencyTracker);
            // This should throw an exception if the login failed
            this.voices = Azure.GetVoicesWithStyles();
            return true;
        }
        catch (Exception e)
        {
            AzureLoginException = e;
            DetailedLog.Error(e, "Failed to initialize Azure client");
            return false;
        }
    }
}