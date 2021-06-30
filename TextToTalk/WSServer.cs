using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using WebSocketSharp.Server;

namespace TextToTalk
{
    public class WsServer
    {
        private readonly WebSocketServer server;
        private readonly ServerBehavior behavior;

        public int Port { get; } = FreeTcpPort();
        public bool Active { get; private set; }

        public WsServer()
        {
            this.server = new WebSocketServer($"ws://localhost:{Port}");
            this.behavior = new ServerBehavior();
            this.server.AddWebSocketService("/Messages", () => this.behavior);
        }

        public void Broadcast(Gender speakerGender, string message)
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var ipcMessage = new IpcMessage(IpcMessageType.Say, message, speakerGender);
            this.behavior.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }

        public void Cancel()
        {
            if (!Active) throw new InvalidOperationException("Server is not active!");

            var ipcMessage = new IpcMessage(IpcMessageType.Cancel, string.Empty, Gender.None);
            this.behavior.SendMessage(JsonConvert.SerializeObject(ipcMessage));
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
                Send(message);
            }
            
            // Enable re-use of a websocket if the client disconnects
            protected override void OnClose(WebSocketSharp.CloseEventArgs e)
            {
                base.OnClose(e);

                var targetType = typeof(WebSocketBehavior);
                var baseWebsocket = targetType.GetField("_websocket", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                baseWebsocket?.SetValue(this, null);
            }
        }

        [Serializable]
        private class IpcMessage
        {
            public string Type { get; set; }
            public string Payload { get; set; }
            public sbyte SpeakerGender { get; set; }

            public IpcMessage(IpcMessageType type, string payload, Gender speakerGender)
            {
                Type = type.ToString();
                Payload = payload;
                SpeakerGender = (sbyte)speakerGender;
            }
        }

        private enum IpcMessageType
        {
            Say,
            Cancel,
        }
    }
}
