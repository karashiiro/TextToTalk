using ImGuiNET;
using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using R3;
using TextToTalk.UI;

namespace TextToTalk.Backends.Websocket
{
    public class WebsocketBackend : VoiceBackend
    {
        private static readonly Vector4 Red = new(1, 0, 0, 1);
        private static readonly Vector4 Hint = new(1.0f, 1.0f, 1.0f, 0.6f);
        private static readonly Vector4 Azure = new(0.24f, 0.44f, 0.6f, 1.0f);
        private static readonly Vector4 AzureHovered = new(0.21f, 0.49f, 0.7f, 1.0f);
        private static readonly Vector4 AzureActive = new(0.16f, 0.52f, 0.8f, 1.0f);

        private readonly WSServer wsServer;
        private readonly PluginConfiguration config;

        private readonly ReplaySubject<int> failedToBindPort;

        private bool dirtyConfig;

        public WebsocketBackend(PluginConfiguration config, IClientState clientState)
        {
            this.config = config;
            this.failedToBindPort = new ReplaySubject<int>(1);

            var messageFactory = new IpcMessageFactory(clientState, config);
            try
            {
                this.wsServer = new WSServer(this.config, messageFactory);
            }
            catch (Exception e) when (e is SocketException or ArgumentOutOfRangeException)
            {
                this.wsServer = new WSServer(this.config, messageFactory, 0);
                this.failedToBindPort.OnNext(this.config.WebsocketPort);
            }

            this.wsServer.Start();
        }

        public Observable<int> OnFailedToBindPort()
        {
            return this.failedToBindPort;
        }

        public override void Say(SayRequest request)
        {
            try
            {
                this.wsServer.Broadcast(request);
                DetailedLog.Debug($"Sent message \"{request.Text}\" on WebSocket server.");
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "Failed to send message over Websocket.");
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
            DrawPortConfig();
            DrawAddressConfig();

            ImGui.Spacing();
            DrawServerStatus();

            ImGui.Spacing();
            DrawServerRestart();
        }

        private void DrawPortConfig()
        {
            var port = this.config.WebsocketPort;
            var portStr = port.ToString();
            if (ImGui.InputText("Port", ref portStr, 5, ImGuiInputTextFlags.CharsDecimal))
            {
                if (int.TryParse(portStr, out var newPort))
                {
                    this.config.WebsocketPort = newPort;
                    this.dirtyConfig = true;
                    this.config.Save();
                }
                else
                {
                    ImGui.TextColored(Red, "Failed to parse port!");
                }
            }
        }

        private void DrawAddressConfig()
        {
            var address = this.config.WebsocketAddress ?? "";
            if (ImGui.InputTextWithHint($"Address##{MemoizedId.Create()}", "localhost", ref address, 40))
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    this.config.WebsocketAddress = null;
                    this.dirtyConfig = true;
                    this.config.Save();
                }
                else if (IPAddress.TryParse(address, out _))
                {
                    this.config.WebsocketAddress = address;
                    this.dirtyConfig = true;
                    this.config.Save();
                }
                else
                {
                    ImGui.TextColored(Red, "Failed to parse address!");
                }
            }

            ImGui.TextColored(Hint, "IPv6 address formats are not currently supported.");
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
                    this.wsServer.RestartWithConnection(
                        IPAddress.TryParse(this.config.WebsocketAddress, out var ip) ? ip : null,
                        this.config.WebsocketPort);
                    this.dirtyConfig = false;
                });
            }
        }

        private static void ImCatchServerRestart(Action fn)
        {
            try
            {
                fn();
            }
            catch (ArgumentOutOfRangeException)
            {
                ImGui.TextColored(Red, "Port out of range");
            }
            catch (SocketException)
            {
                ImGui.TextColored(Red, "Port already taken");
            }
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            // It's not possible to implement this correctly here.
            return TextSource.None;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.wsServer.Dispose();
                this.failedToBindPort.Dispose();
            }
        }
    }
}