using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace TextToTalk.Backends.Websocket;

public class WSServer : IDisposable
{
    private readonly IpcMessageMapper mapper = new();
    private readonly List<ServerBehavior> behaviors = [];

    private WebSocketServer server;
    private int port;

    public IPAddress? Address { get; private set; }

    public string ServiceUrl => $"ws://{Address?.ToString() ?? "localhost"}:{Port}";
    public string ServicePath => "/Messages";

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

    public WSServer(IWebsocketConfigProvider configProvider, int? overridePort = null)
    {
        Address = configProvider.GetAddress();
        Port = overridePort ?? configProvider.GetPort();

        this.server = new WebSocketServer(ServiceUrl);
        this.server.AddWebSocketService<ServerBehavior>(ServicePath, b => this.behaviors.Add(b));
    }

    public void Dispose()
    {
        Stop();
        this.server.RemoveWebSocketService(ServicePath);
    }

    public void Broadcast(SayRequest request)
    {
        if (!Active) throw new InvalidOperationException("Server is not active!");

        var ipcMessage = this.mapper.MapSayRequest(request);
        foreach (var behavior in this.behaviors)
        {
            behavior.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }
    }

    public void CancelAll() => Cancel(TextSource.None);

    public void Cancel(TextSource source)
    {
        if (!Active) throw new InvalidOperationException("Server is not active!");

        var ipcMessage = new IpcMessage(IpcMessageType.Cancel, source);
        foreach (var behavior in this.behaviors)
        {
            behavior.SendMessage(JsonConvert.SerializeObject(ipcMessage));
        }
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

    public void RestartWithConnection(IPAddress? newAddress, int newPort)
    {
        Port = newPort;
        Address = newAddress;
        Stop();
        this.behaviors.Clear();
        this.server.RemoveWebSocketService(ServicePath);
        this.server = new WebSocketServer(ServiceUrl);
        this.server.AddWebSocketService<ServerBehavior>(ServicePath, b => this.behaviors.Add(b));
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
}