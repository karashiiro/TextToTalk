using System;

namespace TextToTalk.Backends;

public interface IPlaybackDeviceProvider
{
    Guid GetDeviceId();

    void SetDevice(Guid deviceId);
}