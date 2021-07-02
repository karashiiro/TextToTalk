using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends.Websocket
{
    public class WebsocketBackend : VoiceBackend
    {
        private static readonly Vector4 Red = new(1, 0, 0, 1);

        private readonly WSServer wsServer;
        private readonly PluginConfiguration config;

        public WebsocketBackend(PluginConfiguration config, SharedState sharedState)
        {
            try
            {
                this.wsServer = new WSServer(this.config.WebsocketPort);
                this.wsServer.Start();
            }
            catch (Exception e) when (e is SocketException or ArgumentOutOfRangeException)
            {
                this.wsServer = new WSServer(0);
                sharedState.WSFailedToBindPort = true;
            }

            this.config = config;
        }

        public override void Say(Gender gender, string text)
        {
            try
            {
                this.wsServer.Broadcast(gender, text);
#if DEBUG
                PluginLog.Log("Sent message {0} on WebSocket server.", text);
#endif
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "Failed to send message over Websocket.");
            }
        }

        public override void CancelSay()
        {
            this.wsServer.Cancel();
            PluginLog.Log("Canceled TTS over WebSocket server.");
        }

        public override void DrawSettings(ImExposedFunctions helpers)
        {
            var port = this.config.WebsocketPort;
            var portBytes = Encoding.UTF8.GetBytes(port.ToString());
            var inputBuffer = new byte[6]; // One extra byte for the null terminator
            Array.Copy(portBytes, inputBuffer, portBytes.Length > inputBuffer.Length ? inputBuffer.Length : portBytes.Length);

            if (ImGui.InputText("Port##TTTVoice12", inputBuffer, (uint)inputBuffer.Length, ImGuiInputTextFlags.CharsDecimal))
            {
                if (int.TryParse(Encoding.UTF8.GetString(inputBuffer), out var newPort))
                {
                    try
                    {
                        this.wsServer.RestartWithPort(newPort);
                        this.config.WebsocketPort = newPort;
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
                else
                {
                    PluginLog.LogError("Failed to parse port!");
                }
            }

            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), $"{(this.wsServer.Active ? "Started" : "Will start")} on ws://localhost:{this.wsServer.Port}");

            ImGui.Spacing();

            if (ImGui.Button("Restart server##TTTVoice13"))
            {
                this.wsServer.RestartWithPort(this.config.WebsocketPort);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.wsServer.Stop();
            }
        }
    }
}