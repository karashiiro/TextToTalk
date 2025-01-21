using System;

namespace TextToTalk.Backends;

public class PlaybackDeviceProvider(PluginConfiguration config) : IPlaybackDeviceProvider
{
    public Guid GetDeviceId()
    {
        return config.PlaybackDeviceId;
    }

    public void SetDevice(Guid deviceId)
    {
        config.PlaybackDeviceId = deviceId;
    }
}