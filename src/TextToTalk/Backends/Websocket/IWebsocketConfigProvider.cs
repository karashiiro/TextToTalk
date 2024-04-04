using System.Net;

namespace TextToTalk.Backends.Websocket;

/// <summary>
/// Interface for getting config options used by the WebSocket backend. This avoids coupling <see cref="WSServer"/>
/// to <see cref="PluginConfiguration"/>.
/// </summary>
public interface IWebsocketConfigProvider
{
    /// <summary>
    /// Returns the listening port.
    /// </summary>
    /// <returns></returns>
    int GetPort();

    /// <summary>
    /// Returns the listening address.
    /// </summary>
    /// <returns></returns>
    IPAddress? GetAddress();
}