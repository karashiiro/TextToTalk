using System.Collections.Generic;
using NAudio.Wave;

namespace TextToTalk;

public static class AudioDevices
{
    // Cache device list at init to avoid bugs due to it changing at runtime
    public static readonly IEnumerable<DirectSoundDeviceInfo> DeviceList = DirectSoundOut.Devices;
}