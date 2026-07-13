using Dalamud.Bindings.ImGui;
using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using TextToTalk.Backends.Websocket;
using TextToTalk.Services;
using TextToTalk.UI;

namespace TextToTalk.Backends.Megaphone;

public class MegaphoneBackend : VoiceBackend
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly Vector4 Hint = new(1.0f, 1.0f, 1.0f, 0.6f);
    private static readonly Vector4 Azure = new(0.24f, 0.44f, 0.6f, 1.0f);
    private static readonly Vector4 AzureHovered = new(0.21f, 0.49f, 0.7f, 1.0f);
    private static readonly Vector4 AzureActive = new(0.16f, 0.52f, 0.8f, 1.0f);

    private const string InstallUrl = "https://github.com/barrcodes/xiv-megaphone";

    private readonly WSServer wsServer;
    private readonly PluginConfiguration config;

    private bool dirtyConfig;
    private Exception? lastException;

    public MegaphoneBackend(PluginConfiguration config, INotificationService notificationService)
    {
        this.config = config;
        try
        {
            this.wsServer = new WSServer(new MegaphoneConfigProvider(config));
        }
        catch (Exception e)
        {
            notificationService.NotifyError(
                $"TextToTalk failed to bind the Megaphone server to port {config.MegaphonePort}.",
                "Please close the owner of that port and reload the Megaphone server, or select a different port.");
            this.wsServer = new WSServer(new MegaphoneConfigProvider(config), 0);
        }
    }

    public override void Start()
    {
        try
        {
            this.wsServer.Start();
        }
        catch (Exception e)
        {
            lastException = e;
        }
    }

    public override void Stop()
    {
        this.wsServer.Stop();
    }

    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }

    public override void Say(SayRequest request)
    {
        try
        {
            this.wsServer.Broadcast(request);
            DetailedLog.Debug($"Sent message \"{request.Text}\" on Megaphone server.");
        }
        catch (Exception e)
        {
            DetailedLog.Error(e, "Failed to send message over Megaphone.");
        }
    }

    public override void CancelAllSpeech()
    {
        this.wsServer.CancelAll();
    }

    public override void CancelSay(TextSource source)
    {
        this.wsServer.Cancel(source);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        DrawInstallLink();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawPortConfig();
        ImGui.Spacing();

        DrawServerRestart();
        ImGui.Spacing();

        DrawServerStatus();
    }

    private void DrawPortConfig()
    {
        var port = this.config.MegaphonePort;
        var portStr = port.ToString();

        var didUpdate = ImGui.InputText("Port", ref portStr, 5, ImGuiInputTextFlags.CharsDecimal);

        if (int.TryParse(portStr, out var newPort))
        {
            if (!IsValidPort(newPort))
            {
                ImGui.TextColored(Red, "Port is out of range [0, 65535]");
            }
            else if (didUpdate)
            {
                this.config.MegaphonePort = newPort;
                this.dirtyConfig = true;
                this.config.Save();
            }
        }
        else
        {
            ImGui.TextColored(Red, "Unable to parse port.");
        }
    }

    private void DrawInstallLink()
    {
        ImGui.TextColored(Hint, "Install the Megaphone Dalamud plugin:");
        if (ImGui.Button($"Open Install Guide##{MemoizedId.Create()}"))
        {
            Dalamud.Utility.Util.OpenLink(InstallUrl);
        }
    }

    private void DrawServerStatus()
    {
        var fullServiceUrl = this.wsServer.ServiceUrl + this.wsServer.ServicePath;
        ImGui.TextColored(Hint, $"{(this.wsServer.Active ? "Started" : "Will start")} on {fullServiceUrl}");
    }

    private void DrawServerRestart()
    {
        using var bcAzure = ImRaii.PushColor(ImGuiCol.Button, Azure, this.dirtyConfig);
        using var bcAzureHovered = ImRaii.PushColor(ImGuiCol.ButtonHovered, AzureHovered, this.dirtyConfig);
        using var bcAzureActive = ImRaii.PushColor(ImGuiCol.ButtonActive, AzureActive, this.dirtyConfig);
        if (ImGui.Button($"Restart server##{MemoizedId.Create()}"))
        {
            ImCatchServerRestart(() =>
            {
                this.wsServer.RestartWithConnection(null, this.config.MegaphonePort);
                this.dirtyConfig = false;
            });
        }

        ImLastError();
    }

    private void ImCatchServerRestart(Action fn)
    {
        try
        {
            lastException = null;
            fn();
        }
        catch (Exception e)
        {
            lastException = e;
        }
    }

    private void ImLastError()
    {
        if (lastException == null)
        {
            return;
        }

        switch (lastException)
        {
            case ArgumentOutOfRangeException:
                ImGui.TextColored(Red, "Port is out of range [0, 65535]");
                break;
            case SocketException:
                ImGui.TextColored(Red, "Port is already in use by another server.");
                break;
            default:
                ImGui.TextColored(Red, $"Unknown error: {lastException.Message}");
                break;
        }
    }

    private static bool IsValidPort(int port) => port is >= 0 and <= 65535;

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return TextSource.None;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.wsServer.Dispose();
        }
    }

    private class MegaphoneConfigProvider : Websocket.IWebsocketConfigProvider
    {
        private readonly PluginConfiguration config;

        public MegaphoneConfigProvider(PluginConfiguration config)
        {
            this.config = config;
        }

        public int GetPort() => config.MegaphonePort;

        public IPAddress? GetAddress() => null;
    }
}