using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Game.Text;
using Moq;
using Newtonsoft.Json;
using R3;
using TextToTalk.Backends;
using TextToTalk.Backends.Websocket;
using WebSocketSharp;
using Xunit;

namespace TextToTalk.Tests.Backends.Websocket;

public class WSServerTests
{
    [Fact]
    public void Ctor_WithNullAddress_DoesNotThrow()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider);
    }

    [Fact]
    public void Ctor_WithValidAddress_DoesNotThrow()
    {
        var configProvider = new Mock<IWebsocketConfigProvider>();
        configProvider.Setup(x => x.GetAddress()).Returns(IPAddress.Any);
        using var server = new WSServer(configProvider.Object);
    }

    [Fact]
    public void Ctor_WithValidPort_DoesNotThrow()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
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
        using var server = new WSServer(configProvider, 0);
        Assert.False(server.Active);
    }

    [Fact]
    public void Start_MakesServerActive()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        server.Start();
        Assert.True(server.Active);
    }

    [Fact]
    public void Start_Stop_MakesServerInactive()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        server.Start();
        server.Stop();
        Assert.False(server.Active);
    }

    [Fact]
    public void RestartWithConnection_WithValidAddress_ChangesAddress()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        var initialAddress = server.Address;
        server.RestartWithConnection(IPAddress.Any, 0);
        var finalAddress = server.Address;
        Assert.Equal(IPAddress.Any, finalAddress);
        Assert.NotEqual(initialAddress, finalAddress);
    }

    [Fact]
    public void RestartWithConnection_WithValidPort_ChangesPort()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        var initialPort = server.Port;
        server.RestartWithConnection(null, 0);
        var finalPort = server.Port;
        Assert.NotEqual(initialPort, finalPort);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void RestartWithConnection_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider);
        Assert.Throws<ArgumentOutOfRangeException>(() => server.RestartWithConnection(null, port));
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    public async Task Broadcast_WhileActive_BroadcastsMessage(TextSource source)
    {
        await RunStandardBroadcastTest(null, source);
    }

    [Fact]
    public async Task Broadcast_WithIPv4_BroadcastsMessage()
    {
        await RunStandardBroadcastTest(IPAddress.Any, TextSource.Chat);
    }

    [Fact]
    public async Task Broadcast_WithIPv6_ThrowsArgumentException()
    {
        // websocket-sharp doesn't seem to support IPv6 addresses - this is here as a sanity check
        await Assert.ThrowsAsync<ArgumentException>(() => RunStandardBroadcastTest(IPAddress.IPv6Any, TextSource.Chat));
    }

    private static async Task RunStandardBroadcastTest(IPAddress? address, TextSource source)
    {
        // Set up the server
        var configProvider = new Mock<IWebsocketConfigProvider>();
        configProvider.Setup(x => x.GetAddress()).Returns(address);

        using var server = new WSServer(configProvider.Object, 0);
        server.Start();

        // Set up the client
        using var client = CreateClient(server);

        // Filter for say messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == IpcMessageType.Say.ToString())
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

        server.Broadcast(new SayRequest
        {
            Source = source,
            Voice = preset,
            Speaker = "Speaker",
            Text = "Hello, world!",
            TextTemplate = "Hello, world!",
            Race = "Hyur",
            ChatType = XivChatType.Say,
            Language = ClientLanguage.English,
        });

        // Wait a bit
        await Task.Delay(100);

        // Assert that a say message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[]
        {
            new IpcMessage(IpcMessageType.Say, source)
            {
                Voice = preset,
                Speaker = "Speaker",
                Payload = "Hello, world!",
                PayloadTemplate = "Hello, world!",
                ChatType = (int)XivChatType.Say,
                Language = ClientLanguage.English.ToString(),
            },
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
        using var server = new WSServer(configProvider, 0);
        Assert.False(server.Active);

        var preset = new WebsocketVoicePreset
        {
            Id = 0,
            EnabledBackend = TTSBackend.Websocket,
            Name = "Some Body",
        };

        Assert.Throws<InvalidOperationException>(() =>
            server.Broadcast(new SayRequest
            {
                Source = source,
                Voice = preset,
                Speaker = "Speaker",
                Text = "Hello, world!",
                TextTemplate = "Hello, world!",
                Race = "Hyur",
                Language = ClientLanguage.English,
            }));
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task CancelAll_WhileActive_BroadcastsCancelMessageWithNoneSource()
    {
        // Set up the server
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        server.Start();

        // Set up the client
        using var client = CreateClient(server);

        // Filter for cancel messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == IpcMessageType.Cancel.ToString())
            .Where(m => m?.Source == TextSource.None.ToString())
            .Select(m => (m!.Type, m.Source))
            .Take(1)
            .ToLiveList();

        // Send the cancel message
        server.CancelAll();

        // Wait a bit
        await Task.Delay(100);

        // Assert that a cancel message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[] { (IpcMessageType.Cancel.ToString(), TextSource.None.ToString()) });
    }

    [Fact]
    public void CancelAll_WhileInactive_ThrowsInvalidOperationException()
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        Assert.False(server.Active);
        Assert.Throws<InvalidOperationException>(() => server.CancelAll());
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
        using var server = new WSServer(configProvider, 0);
        server.Start();

        // Set up the client
        using var client = CreateClient(server);

        // Filter for cancel messages only
        using var list = OnIpcMessage(client)
            .Where(m => m?.Type == IpcMessageType.Cancel.ToString())
            .Select(m => m!.Type)
            .Take(1)
            .ToLiveList();

        // Send the cancel message
        server.Cancel(source);

        // Wait a bit
        await Task.Delay(100);

        // Assert that a cancel message was received
        Assert.True(list.IsCompleted);
        Assert.Equal(list, new[] { IpcMessageType.Cancel.ToString() });
    }

    [Theory]
    [InlineData(TextSource.None)]
    [InlineData(TextSource.Chat)]
    [InlineData(TextSource.AddonTalk)]
    [InlineData(TextSource.AddonBattleTalk)]
    public void Cancel_WhileInactive_ThrowsInvalidOperationException(TextSource source)
    {
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        Assert.False(server.Active);
        Assert.Throws<InvalidOperationException>(() => server.Cancel(source));
    }

    [Fact]
    public async Task ServerBehavior_Supports_Reconnect()
    {
        // Set up the server
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        server.Start();

        // Set up the client
        using var client = CreateClient(server);

        // Send a message
        using var list1 = OnIpcMessage(client).Take(1).ToLiveList();
        server.CancelAll();
        await Task.Delay(100);

        // Confirm that it was received
        Assert.True(list1.IsCompleted);
        Assert.Single(list1);

        // Disconnect from the server
        // ReSharper disable once MethodHasAsyncOverload
        client.Close();

        // Reconnect to the server
        // ReSharper disable once MethodHasAsyncOverload
        client.Connect();

        // Send a message
        using var list2 = OnIpcMessage(client).Take(1).ToLiveList();
        server.CancelAll();
        await Task.Delay(100);

        // Confirm that it was received
        Assert.True(list2.IsCompleted);
        Assert.Single(list2);
    }

    [Fact]
    public async Task ServerBehavior_Supports_MultipleConnections()
    {
        // Set up the server
        var configProvider = Mock.Of<IWebsocketConfigProvider>();
        using var server = new WSServer(configProvider, 0);
        server.Start();

        // Set up a client
        using var client1 = CreateClient(server);

        // Set up a second client
        using var client2 = CreateClient(server);

        // Send a message
        using var list1 = OnIpcMessage(client1).Take(1).ToLiveList();
        using var list2 = OnIpcMessage(client2).Take(1).ToLiveList();
        server.CancelAll();
        await Task.Delay(100);

        // Confirm that it was received by the second client (checking it first in case it clobbered the other one)
        Assert.True(list2.IsCompleted);
        Assert.Single(list2);

        // Confirm that it was received by the first client
        Assert.True(list1.IsCompleted);
        Assert.Single(list1);
    }

    private static WebSocket CreateClient(WSServer server)
    {
        var client = new WebSocket($"ws://localhost:{server.Port}/Messages");
        client.Connect();
        return client;
    }

    private static Observable<IpcMessage?> OnIpcMessage(WebSocket client)
    {
        var onMessage = Observable.FromEventHandler<MessageEventArgs>(
            handler => client.OnMessage += handler,
            handler => client.OnMessage -= handler);
        return onMessage.Select(m => JsonConvert.DeserializeObject<IpcMessage>(m.e.Data));
    }
}