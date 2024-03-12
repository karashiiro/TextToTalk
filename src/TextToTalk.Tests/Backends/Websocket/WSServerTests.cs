using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using R3;
using TextToTalk.Backends;
using TextToTalk.Backends.Websocket;
using WebSocketSharp;
using Xunit;

namespace TextToTalk.Tests.Backends.Websocket;

public class WSServerTests : IDisposable
{
    private WSServer? server;

    public void Dispose()
    {
        server?.Stop();
    }

    [Fact]
    public void Ctor_WithValidPort_DoesNotThrow()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Ctor_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        Assert.Throws<ArgumentOutOfRangeException>(() => new WSServer(configProvider, port));
    }

    [Fact]
    public void Ctor_StartsInactive()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        Assert.False(this.server.Active);
    }

    [Fact]
    public void Start_MakesServerActive()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        this.server.Start();
        Assert.True(this.server.Active);
    }

    [Fact]
    public void Start_Stop_MakesServerInactive()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        this.server.Start();
        this.server.Stop();
        Assert.False(this.server.Active);
    }

    [Fact]
    public void RestartWithPort_WithValidPort_ChangesPort()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        var initialPort = this.server.Port;
        this.server.RestartWithPort(0);
        var finalPort = this.server.Port;
        Assert.NotEqual(initialPort, finalPort);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void RestartWithPort_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => this.server.RestartWithPort(port));
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task Broadcast_WhileActive_BroadcastsMessage(TextSource source)
    {
        // Set up the server
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        this.server.Start();

        // Set up the client
        using var client = CreateClient();

        // Filter for say messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == WSServer.IpcMessageType.Say.ToString())
            .Select(m => m!)
            .Take(1)
            .ToLiveList();

        // Send the message
        var preset = new VoicePreset
        {
            Id = 0,
            EnabledBackend = TTSBackend.Websocket,
            Name = "Some Body",
        };

        this.server.Broadcast("Speaker", source, preset, "Hello, world!");

        // Wait a bit
        await Task.Delay(100);

        // Assert that a say message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[]
        {
            new WSServer.IpcMessage("Speaker", WSServer.IpcMessageType.Say, "Hello, world!", preset, source, false),
        });
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    public void Broadcast_WhileInactive_ThrowsInvalidOperationException(TextSource source)
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        Assert.False(this.server.Active);

        var preset = new WebsocketVoicePreset
        {
            Id = 0,
            EnabledBackend = TTSBackend.Websocket,
            Name = "Some Body",
        };

        Assert.Throws<InvalidOperationException>(
            () => this.server.Broadcast("Speaker", source, preset, "Hello, world!"));
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task Broadcast_RespectsStutterConfig(TextSource source)
    {
        // Mock the config value
        var configProvider = new Mock<IWebsocketConfigProvider>();
        configProvider.Setup(p => p.AreStuttersRemoved()).Returns(true);

        // Set up the server
        this.server = new WSServer(configProvider.Object, 0);
        this.server.Start();

        // Set up the client
        using var client = CreateClient();

        // Filter for say messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == WSServer.IpcMessageType.Say.ToString())
            .Select(m => m!)
            .Take(1)
            .ToLiveList();

        // Send the message
        var preset = new VoicePreset
        {
            Id = 0,
            EnabledBackend = TTSBackend.Websocket,
            Name = "Some Body",
        };

        this.server.Broadcast("Speaker", source, preset, "Hello, world!");

        // Wait a bit
        await Task.Delay(100);

        // Assert that a say message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[]
        {
            new WSServer.IpcMessage("Speaker", WSServer.IpcMessageType.Say, "Hello, world!", preset, source, true),
        });

        configProvider.Verify();
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task CancelAll_WhileActive_BroadcastsCancelMessageWithNoneSource()
    {
        // Set up the server
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        this.server.Start();

        // Set up the client
        using var client = CreateClient();

        // Filter for cancel messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == WSServer.IpcMessageType.Cancel.ToString())
            .Where(m => m?.Source == TextSource.None.ToString())
            .Select(m => (m!.Type, m.Source))
            .Take(1)
            .ToLiveList();

        // Send the cancel message
        this.server.CancelAll();

        // Wait a bit
        await Task.Delay(100);

        // Assert that a cancel message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[] { (WSServer.IpcMessageType.Cancel.ToString(), TextSource.None.ToString()) });
    }

    [Fact]
    public void CancelAll_WhileInactive_ThrowsInvalidOperationException()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        Assert.False(this.server.Active);
        Assert.Throws<InvalidOperationException>(() => this.server.CancelAll());
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task Cancel_WhileActive_BroadcastsCancelMessage(TextSource source)
    {
        // Set up the server
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        this.server.Start();

        // Set up the client
        using var client = CreateClient();

        // Filter for cancel messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == WSServer.IpcMessageType.Cancel.ToString())
            .Select(m => m!.Type)
            .Take(1)
            .ToLiveList();

        // Send the cancel message
        this.server.Cancel(source);

        // Wait a bit
        await Task.Delay(100);

        // Assert that a cancel message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[] { WSServer.IpcMessageType.Cancel.ToString() });
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    public void Cancel_WhileInactive_ThrowsInvalidOperationException(TextSource source)
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        this.server = new WSServer(configProvider, 0);
        Assert.False(this.server.Active);
        Assert.Throws<InvalidOperationException>(() => this.server.Cancel(source));
    }

    private WebSocket CreateClient()
    {
        ArgumentNullException.ThrowIfNull(this.server);

        var client = new WebSocket($"ws://localhost:{this.server.Port}/Messages");
        // ReSharper disable once MethodHasAsyncOverload
        client.Connect();
        return client;
    }

    private static Observable<WSServer.IpcMessage?> OnIpcMessage(WebSocket client)
    {
        var onMessage = Observable.FromEventHandler<MessageEventArgs>(
            handler => client.OnMessage += handler,
            handler => client.OnMessage -= handler);
        return onMessage.Select(m => JsonConvert.DeserializeObject<WSServer.IpcMessage>(m.e.Data));
    }
}