using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Moq;
using TextToTalk.Backends.Websocket;
using Xunit;

namespace TextToTalk.Tests.Backends.Websocket;

public class IpcMessageFactoryTests
{
    private const string FirstName = "Someone";
    private const string LastName = "Special";
    private const string FullName = $"{FirstName} {LastName}";

    [Fact]
    public void Ctor_DoesNotThrow()
    {
        var (clientState, configProvider) = CreateMocks();
        _ = new IpcMessageFactory(clientState.Object, configProvider.Object);
    }

    [Fact]
    public void CreateBroadcast_Works()
    {
        var (clientState, configProvider) = CreateMocks();

        var messageFactory = new IpcMessageFactory(clientState.Object, configProvider.Object);
        var actual = messageFactory.CreateBroadcast(FullName, TextSource.Chat, new VoicePreset(),
            $"{FullName} says something to them.", null, null);

        Assert.Equal(IpcMessageType.Say.ToString(), actual.Type);
    }

    [Fact]
    public void CreateBroadcast_WhenPlayerNull_DoesNotGetPayloadTokens()
    {
        var (clientState, configProvider) = CreateMocks(localPlayer: null);

        var messageFactory = new IpcMessageFactory(clientState.Object, configProvider.Object);
        var actual = messageFactory.CreateBroadcast(FullName, TextSource.Chat, new VoicePreset(),
            $"{FullName} says something to them.", null, null);

        Assert.Equal($"{FullName} says something to them.", actual.PayloadTemplate);
    }

    [Fact]
    public void CreateBroadcast_ReplacesFullNamesInPayloadTemplate()
    {
        var (clientState, configProvider) = CreateMocks();

        var messageFactory = new IpcMessageFactory(clientState.Object, configProvider.Object);
        var actual = messageFactory.CreateBroadcast(FullName, TextSource.Chat, new VoicePreset(),
            $"{FullName} says something to them.", null, null);

        Assert.Equal("{{FULL_NAME}} says something to them.", actual.PayloadTemplate);
    }

    [Fact]
    public void CreateBroadcast_ReplacesFirstNamesInPayloadTemplate()
    {
        var (clientState, configProvider) = CreateMocks();

        var messageFactory = new IpcMessageFactory(clientState.Object, configProvider.Object);
        var actual = messageFactory.CreateBroadcast(FullName, TextSource.Chat, new VoicePreset(),
            $"{FirstName} says something to them.", null, null);

        Assert.Equal("{{FIRST_NAME}} says something to them.", actual.PayloadTemplate);
    }

    [Fact]
    public void CreateBroadcast_ReplacesLastNamesInPayloadTemplate()
    {
        var (clientState, configProvider) = CreateMocks();

        var messageFactory = new IpcMessageFactory(clientState.Object, configProvider.Object);
        var actual = messageFactory.CreateBroadcast(FullName, TextSource.Chat, new VoicePreset(),
            $"{LastName} says something to them.", null, null);

        Assert.Equal("{{LAST_NAME}} says something to them.", actual.PayloadTemplate);
    }

    [Fact]
    public void CreateCancel_Works()
    {
        var (clientState, configProvider) = CreateMocks();

        var messageFactory = new IpcMessageFactory(clientState.Object, configProvider.Object);
        var actual = messageFactory.CreateCancel(TextSource.Chat);

        Assert.Equal(IpcMessageType.Cancel.ToString(), actual.Type);
    }

    private static (Mock<IClientState>, Mock<IWebsocketConfigProvider>) CreateMocks()
    {
        return CreateMocks(CreatePlayer());
    }

    private static (Mock<IClientState>, Mock<IWebsocketConfigProvider>) CreateMocks(PlayerCharacter? localPlayer)
    {
        var clientState = new Mock<IClientState>();
        clientState.Setup(x => x.LocalPlayer).Returns(localPlayer);

        var configProvider = new Mock<IWebsocketConfigProvider>();

        return (clientState, configProvider);
    }

    private static unsafe PlayerCharacter CreatePlayer()
    {
        // Allocate a GC-stable player-sized buffer
        var buffer = GC.AllocateArray<byte>(0x2F80);
        var bufferPtr = (nint)Unsafe.AsPointer(ref buffer[0]);

        // Get the name in bytes
        var utf8Name = Encoding.UTF8.GetBytes(FullName);

        // Create a player without calling its ctor
        var localPlayer = (PlayerCharacter)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCharacter));

        // Assign its address to our buffer
        var addressPropertyName = nameof(localPlayer.Address);
        var addressProperty = typeof(GameObject).GetProperty(addressPropertyName);
        ArgumentNullException.ThrowIfNull(addressProperty);
        addressProperty.SetValue(localPlayer, bufferPtr);

        // Copy the player name into the appropriate struct field
        var gameObject = *(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)localPlayer.Address;
        var nameFieldName = nameof(gameObject.Name);
        var nameFieldOffset = gameObject.GetType().GetField(nameFieldName)?.GetCustomAttribute<FieldOffsetAttribute>();
        ArgumentNullException.ThrowIfNull(nameFieldOffset);
        utf8Name.CopyTo(buffer, nameFieldOffset.Value);

        // Validate that we've done all this correctly
        Assert.Equal(FullName, localPlayer.Name.TextValue);

        return localPlayer;
    }
}