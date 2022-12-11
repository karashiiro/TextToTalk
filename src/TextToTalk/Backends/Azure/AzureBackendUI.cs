using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using ImGuiNET;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureBackendUI
{
    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly LexiconManager lexiconManager;

    private readonly Func<AzureClient> getAzure;
    private readonly Action<AzureClient> setAzure;
    private readonly Func<IList<string>> getVoices;
    private readonly Action<IList<string>> setVoices;

    private string accessKey = string.Empty;
    private string secretKey = string.Empty;
    
    public AzureBackendUI(PluginConfiguration config, LexiconManager lexiconManager, HttpClient http,
        Func<AzureClient> getAzure, Action<AzureClient> setAzure, Func<IList<string>> getVoices,
        Action<IList<string>> setVoices)
    {
        this.getAzure = getAzure;
        this.setAzure = setAzure;
        this.getVoices = getVoices;
        this.setVoices = setVoices;

        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");
        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.config = config;
        this.lexiconComponent =
            new LexiconComponent(lexiconManager, lexiconRepository, config, Array.Empty<string>);
        this.lexiconManager = lexiconManager;

        var credentials = AzureCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.accessKey = credentials.UserName;
            this.secretKey = credentials.Password;

            var regionEndpoint = "uswest";
            AzureLogin(regionEndpoint);
        }
    }
    
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        ImGui.InputTextWithHint("##TTTPollyAccessKey", "Access key", ref this.accessKey, 100,
            ImGuiInputTextFlags.Password);
        ImGui.InputTextWithHint("##TTTPollySecretKey", "Secret key", ref this.secretKey, 100,
            ImGuiInputTextFlags.Password);

        if (ImGui.Button("Save and Login##TTTSavePollyAuth"))
        {
            var username = Whitespace.Replace(this.accessKey, "");
            var password = Whitespace.Replace(this.secretKey, "");
            AzureCredentialManager.SaveCredentials(username, password);

            var regionEndpoint = "uswest";
            AzureLogin(regionEndpoint);
        }
    }
    
    private void AzureLogin(string region)
    {
        var azure = this.getAzure.Invoke();
        azure?.Dispose();
        try
        {
            PluginLog.Log($"Logging into Azure region {region}.");
            azure = new AzureClient(this.accessKey, region, this.lexiconManager);
            var voices = azure.GetVoices();
            this.setAzure.Invoke(azure);
            this.setVoices.Invoke(voices);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Failed to initialize Azure client.");
            AzureCredentialManager.DeleteCredentials();
        }
    }
}