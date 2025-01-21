using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Polly;

public class PollyBackendUIModel : IDisposable
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly PluginConfiguration config;
    private readonly IPlaybackDeviceProvider playbackDeviceProvider;
    private readonly LexiconManager lexiconManager;

    private List<Voice> voices;

    private PollyKeyPair keyPair;
    private bool didVoicePresetChange;

    /// <summary>
    /// Gets the currently-instantiated Polly client instance.
    /// </summary>
    public PollyClient? Polly { get; private set; }

    /// <summary>
    /// Gets the exception thrown by the most recent login, or null if the login was successful.
    /// </summary>
    public Exception? PollyLoginException { get; private set; }

    /// <summary>
    /// Gets the valid voices for the current voice engine.
    /// </summary>
    public IReadOnlyList<Voice> CurrentEngineVoices => this.voices;

    /// <summary>
    /// Gets the system names of all available AWS regions.
    /// </summary>
    public string[] Regions { get; } = RegionEndpoint.EnumerableAllRegions.Select(r => r.SystemName).ToArray();

    /// <summary>
    /// Gets the available voice engines for AWS Polly.
    /// </summary>
    public string[] Engines { get; } = { Engine.Neural, Engine.Standard };

    public PollyBackendUIModel(PluginConfiguration config, LexiconManager lexiconManager,
        IPlaybackDeviceProvider playbackDeviceProvider)
    {
        this.config = config;
        this.lexiconManager = lexiconManager;
        this.playbackDeviceProvider = playbackDeviceProvider;
        this.voices = new List<Voice>();

        this.keyPair = new PollyKeyPair();
        var credentials = PollyCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.keyPair.AccessKey = credentials.UserName;
            this.keyPair.SecretKey = credentials.Password;

            TryPollyLogin(GetCurrentRegion());
        }
    }

    /// <summary>
    /// Gets the client's current credentials.
    /// </summary>
    /// <returns>The client's current credentials.</returns>
    public PollyKeyPair GetKeyPair()
        => this.keyPair;

    /// <summary>
    /// Logs in with the provided credentials.
    /// </summary>
    /// <param name="accessKey">The client's access key.</param>
    /// <param name="secretKey">The client's secret access key.</param>
    public void LoginWith(string accessKey, string secretKey)
    {
        var username = Whitespace.Replace(accessKey, "");
        var password = Whitespace.Replace(secretKey, "");
        this.keyPair = new PollyKeyPair { AccessKey = username, SecretKey = password };

        if (TryPollyLogin(GetCurrentRegion()))
        {
            // Only save the user's new credentials if the login succeeded
            PollyCredentialManager.SaveCredentials(username, password);
        }
    }

    /// <summary>
    /// Gets the current voice preset.
    /// </summary>
    /// <returns>The current voice preset, or null if no voice preset is selected.</returns>
    public PollyVoicePreset? GetCurrentVoicePreset()
        => this.config.GetCurrentVoicePreset<PollyVoicePreset>();

    /// <summary>
    /// Sets the current voice preset.
    /// </summary>
    /// <param name="id">The preset ID.</param>
    public void SetCurrentVoicePreset(int id)
    {
        this.config.SetCurrentVoicePreset(id);
        this.config.Save();

        this.didVoicePresetChange = true;
    }

    /// <summary>
    /// Gets the client's AWS region endpoint.
    /// </summary>
    /// <returns>The client's region endpoint, or eu-west-1 if no region is configured.</returns>
    public RegionEndpoint GetCurrentRegion()
    {
        return FindRegionEndpoint(this.config.PollyRegion) ?? RegionEndpoint.EUWest1;
    }

    /// <summary>
    /// Sets the client's AWS region endpoint.
    /// </summary>
    /// <param name="systemName">The system name of the region, e.g. eu-west-1.</param>
    public void SetCurrentRegion(string systemName)
    {
        var regionEndpoint = FindRegionEndpoint(systemName);
        if (regionEndpoint == null)
        {
            DetailedLog.Error("Invalid region provided; cannot set region endpoint");
            return;
        }

        this.config.PollyRegion = regionEndpoint.SystemName;
        this.config.Save();
    }

    /// <summary>
    /// Gets the voice engine for the currently-selected voice preset.
    /// </summary>
    /// <returns>The current preset's voice engine, or Engine.Standard if the result was null.</returns>
    public Engine GetCurrentEngine()
    {
        var preset = GetCurrentVoicePreset();
        if (preset == null)
        {
            // This needs to be guarded to avoid spamming the log in the draw loop
            if (this.didVoicePresetChange)
            {
                DetailedLog.Error("Current voice preset is null; can't get voice engine");
                this.didVoicePresetChange = false;
            }

            return Engine.Standard;
        }

        return Engine.FindValue(preset.VoiceEngine);
    }

    /// <summary>
    /// Sets the voice engine for the currently-selected voice preset.
    /// </summary>
    /// <param name="engine">The voice engine to select.</param>
    public void SetCurrentEngine(Engine engine)
    {
        var preset = GetCurrentVoicePreset();
        if (preset == null)
        {
            DetailedLog.Error("Current voice preset is null; can't set voice engine");
            return;
        }

        preset.VoiceEngine = engine;
        this.voices = Polly?.GetVoicesForEngine(engine) ?? new List<Voice>();
        this.config.Save();
    }

    private bool TryPollyLogin(RegionEndpoint regionEndpoint)
    {
        PollyLoginException = null;
        Polly?.Dispose();
        try
        {
            DetailedLog.Info($"Logging into AWS region {regionEndpoint}");
            Polly = new PollyClient(this.keyPair.AccessKey, this.keyPair.SecretKey, regionEndpoint,
                this.lexiconManager, this.playbackDeviceProvider);
            var currentVoicePreset = this.config.GetCurrentVoicePreset<PollyVoicePreset>();
            // This should throw an exception if the login credentials were incorrect
            this.voices = Polly.GetVoicesForEngine(currentVoicePreset?.VoiceEngine ?? Engine.Neural);
            return true;
        }
        catch (Exception e)
        {
            PollyLoginException = e;
            DetailedLog.Error(e, "Failed to initialize AWS client");
            return false;
        }
    }

    private static RegionEndpoint? FindRegionEndpoint(string systemName)
    {
        return RegionEndpoint.EnumerableAllRegions.FirstOrDefault(r => r.SystemName == systemName);
    }

    public void Dispose()
    {
        Polly?.Dispose();
    }
}