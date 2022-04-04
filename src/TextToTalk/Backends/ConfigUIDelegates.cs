using System;

namespace TextToTalk.Backends
{
    public class ConfigUIDelegates : IConfigUIDelegates
    {
        public Action OpenVoiceUnlockerAction { get; init; }

        public void OpenVoiceUnlocker()
        {
            OpenVoiceUnlockerAction?.Invoke();
        }
    }
}