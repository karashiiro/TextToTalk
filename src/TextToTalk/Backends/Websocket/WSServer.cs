using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace TextToTalk.Backends.Websocket
{
    public class WSServer
    {
        private ServerBehavior? behavior;
        private WebSocketServer server;

        private int port;

        public int Port
        {
            get => port;
            private set => port = value switch
            {
                < IPEndPoint.MinPort or > IPEndPoint.MaxPort
                    => throw new ArgumentOutOfRangeException(
                        $"Port must be at least {IPEndPoint.MinPort} and at most {IPEndPoint.MaxPort}."),
                // Using the first free port in case of 0 is conventional
                // and ensures that we know what port is ultimately used.
                // We can't pass 0 to the server anyways, since it throws
                // if the input is less than 1.
                0 => FreeTcpPort(),
                _ => value,
            };
        }

        public bool Active { get; private set; }

        public WSServer(int port)
        {
            Port = port;

            this.server = new WebSocketServer($"ws://localhost:{Port}");
            this.server.AddWebSocketService<ServerBehavior>("/Messages", b => { this.behavior = b; });
        }

        public void Broadcast(string speaker, TextSource source, VoicePreset voice, string message)
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var ipcMessage = new IpcMessage(speaker, IpcMessageType.Say, message, voice, source);
            this.behavior?.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }

        public void CancelAll()
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var ipcMessage = new IpcMessage(string.Empty, IpcMessageType.Cancel, string.Empty, null, TextSource.None);
            this.behavior?.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }

        public void Cancel(TextSource source)
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var ipcMessage = new IpcMessage(string.Empty, IpcMessageType.Cancel, string.Empty, null, source);
            this.behavior?.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }

        public void Start()
        {
            if (Active) return;
            Active = true;
            this.server.Start();
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false;
            this.server.Stop();
        }

        public void RestartWithPort(int newPort)
        {
            Port = newPort;
            Stop();
            this.server = new WebSocketServer($"ws://localhost:{Port}");
            this.server.AddWebSocketService<ServerBehavior>("/Messages", b => { this.behavior = b; });
            Start();
        }

        private static int FreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private class ServerBehavior : WebSocketBehavior
        {
            public void SendMessage(string message)
            {
                if (ConnectionState == WebSocketState.Open)
                {
                    Send(message);
                }
            }

            // Enable re-use of a websocket if the client disconnects
            protected override void OnClose(CloseEventArgs e)
            {
                base.OnClose(e);

                var targetType = typeof(WebSocketBehavior);
                var baseWebsocket = targetType.GetField("_websocket", BindingFlags.Instance | BindingFlags.NonPublic);
                baseWebsocket?.SetValue(this, null);
            }
        }

        [Serializable]
        private class IpcMessage
        {
            /// <summary>
            /// The speaker name.
            /// </summary>
            public string Speaker { get; set; }

            /// <summary>
            /// The message type; refer tp <see cref="IpcMessageType"/> for options.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The message parameter - the spoken text for speech requests, and nothing for cancellations.
            /// </summary>
            public string Payload { get; set; }

            /// <summary>
            /// Speaker voice ID.
            /// </summary>
            public VoicePreset? Voice { get; set; }

            /// <summary>
            /// Text source; refer to <see cref="TextSource"/> for options.
            /// </summary>
            public string Source { get; set; }

            public IpcMessage(string speaker, IpcMessageType type, string payload, VoicePreset? preset, TextSource source)
            {
                Speaker = speaker;
                Type = type.ToString();
                Payload = payload;
                Voice = preset;
                Source = source.ToString();
            }
        }

        private enum IpcMessageType
        {
            Say,
            Cancel,
        }
    }
}