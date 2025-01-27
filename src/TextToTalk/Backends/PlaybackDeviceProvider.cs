using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace TextToTalk.Backends;

public class PlaybackDeviceProvider(PluginConfiguration config) : IPlaybackDeviceProvider
{
    public IList<DirectSoundDeviceInfo> ListDevices()
    {
        return DirectSoundOut.Devices.ToList();
    }

    public Guid GetDeviceId()
    {
        return config.PlaybackDeviceId;
    }

    public void SetDevice(Guid deviceId)
    {
        config.PlaybackDeviceId = deviceId;
        config.Save();
    }
}