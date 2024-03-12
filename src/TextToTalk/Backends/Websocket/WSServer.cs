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

        private readonly IWebsocketConfigProvider configProvider;

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

        public WSServer(IWebsocketConfigProvider configProvider, int port)
        {
            Port = port;

            this.configProvider = configProvider;

            this.server = new WebSocketServer($"ws://localhost:{Port}");
            this.server.AddWebSocketService<ServerBehavior>("/Messages", b => { this.behavior = b; });
        }

        public void Broadcast(string speaker, TextSource source, VoicePreset voice, string message)
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var stuttersRemoved = this.configProvider.AreStuttersRemoved();
            var ipcMessage = new IpcMessage(speaker, IpcMessageType.Say, message, voice, source, stuttersRemoved);
            this.behavior?.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }

        public void CancelAll()
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var stuttersRemoved = this.configProvider.AreStuttersRemoved();
            var ipcMessage = new IpcMessage(string.Empty, IpcMessageType.Cancel, string.Empty, null, TextSource.None,
                stuttersRemoved);
            this.behavior?.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }

        public void Cancel(TextSource source)
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var stuttersRemoved = this.configProvider.AreStuttersRemoved();
            var ipcMessage = new IpcMessage(string.Empty, IpcMessageType.Cancel, string.Empty, null, source,
                stuttersRemoved);
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
        public class IpcMessage : IEquatable<IpcMessage>
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
            /// If stutters were removed from the payload or not.
            /// </summary>
            public bool StuttersRemoved { get; set; }

            /// <summary>
            /// Text source; refer to <see cref="TextSource"/> for options.
            /// </summary>
            public string Source { get; set; }

            public IpcMessage(string speaker, IpcMessageType type, string payload, VoicePreset? preset,
                TextSource source, bool stuttersRemoved)
            {
                Speaker = speaker;
                Type = type.ToString();
                Payload = payload;
                Voice = preset;
                Source = source.ToString();
                StuttersRemoved = stuttersRemoved;
            }

            public bool Equals(IpcMessage? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Speaker == other.Speaker && Type == other.Type && Payload == other.Payload &&
                       Equals(Voice, other.Voice) && StuttersRemoved == other.StuttersRemoved && Source == other.Source;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GetType() == this.GetType() && Equals((IpcMessage)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Speaker, Type, Payload, Voice, StuttersRemoved, Source);
            }
        }

        public enum IpcMessageType
        {
            Say,
            Cancel,
        }
    }
}