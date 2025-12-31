using System;

namespace TextToTalk.Backends
{
    public class ConfigUIDelegates : IConfigUIDelegates
    {
        public Action? OpenVoiceUnlockerAction { get; init; }

        public Action? OpenVoiceStylesWindow { get; init; }

        public void OpenVoiceUnlocker()
        {
            OpenVoiceUnlockerAction?.Invoke();
        }

        public void OpenVoiceStylesConfig()
        {
            OpenVoiceStylesWindow?.Invoke();
        }
    }
}