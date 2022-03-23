using Dalamud.CrystalTower.DependencyInjection;
using Dalamud.CrystalTower.UI;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Net.Http;
using TextToTalk.Backends;
using TextToTalk.Exceptions;
using TextToTalk.Middleware;
using TextToTalk.UngenderedOverrides;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace TextToTalk;

public class Services : IServiceProvider, IDisposable
{
    [PluginService]
    [RequiredVersion("1.0")]
    public DalamudPluginInterface PluginInterface { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public CommandManager Commands { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public ClientState ClientState { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public Framework Framework { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public DataManager Data { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public ChatGui Chat { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public GameGui Gui { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public KeyState Keys { get; init; } = null!;

    [PluginService]
    [RequiredVersion("1.0")]
    public ObjectTable Objects { get; init; } = null!;

    private PluginServiceCollection serviceCollection;

    public object GetService(Type serviceType)
    {
        return this.serviceCollection.GetService(serviceType);
    }

    public T GetService<T>() where T : class
    {
        return this.serviceCollection.GetService<T>();
    }

    public void Dispose()
    {
        this.serviceCollection.Dispose();
    }

    public static Services Create(DalamudPluginInterface pi, PluginConfiguration config)
    {
        var services = pi.Create<Services>() ?? throw new ServiceException("Failed to initialize plugin services.");
        services.serviceCollection = new PluginServiceCollection();

        services.serviceCollection.AddService(services.PluginInterface, shouldDispose: false);
        services.serviceCollection.AddService(services.Commands, shouldDispose: false);
        services.serviceCollection.AddService(services.ClientState, shouldDispose: false);
        services.serviceCollection.AddService(services.Framework, shouldDispose: false);
        services.serviceCollection.AddService(services.Data, shouldDispose: false);
        services.serviceCollection.AddService(services.Chat, shouldDispose: false);
        services.serviceCollection.AddService(services.Gui, shouldDispose: false);
        services.serviceCollection.AddService(services.Keys, shouldDispose: false);
        services.serviceCollection.AddService(services.Objects, shouldDispose: false);

        var sharedState = new SharedState();
        var http = new HttpClient();
        var filters = new MessageHandlerFilters(services, config);
        var backendManager = new VoiceBackendManager(config, http, sharedState);

        services.serviceCollection.AddService(config);
        services.serviceCollection.AddService(sharedState);
        services.serviceCollection.AddService(http);
        services.serviceCollection.AddService(new UngenderedOverrideManager());
        services.serviceCollection.AddService(backendManager);
        services.serviceCollection.AddService(new RateLimiter(() =>
        {
            if (config.MessagesPerSecond == 0)
            {
                return long.MaxValue;
            }

            return (long)(1000f / config.MessagesPerSecond);
        }));
        services.serviceCollection.AddService(filters);
        services.serviceCollection.AddService(new TalkAddonHandler(services.ClientState, services.Gui, services.Data, filters, services.Objects, config, sharedState, backendManager));
        services.serviceCollection.AddService(new ChatMessageHandler(filters, services.Objects, config, sharedState));
        services.serviceCollection.AddService(new WindowManager(services.serviceCollection));

        return services;
    }
}