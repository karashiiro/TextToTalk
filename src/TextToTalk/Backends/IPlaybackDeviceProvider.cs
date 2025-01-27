using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace TextToTalk.Backends;

public interface IPlaybackDeviceProvider
{
    IList<DirectSoundDeviceInfo> ListDevices();

    Guid GetDeviceId();

    void SetDevice(DirectSoundDeviceInfo device) => SetDevice(device.Guid);

    void SetDevice(Guid deviceId);
}