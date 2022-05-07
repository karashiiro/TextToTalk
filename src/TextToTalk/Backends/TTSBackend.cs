using System;

namespace TextToTalk.Backends
{
    public enum TTSBackend
    {
        System,
        Websocket,
        AmazonPolly,
        Uberduck,
    }

    public static class TTSBackendExtensions
    {
        public static string GetFormattedName(this TTSBackend backend)
        {
            return backend switch
            {
                TTSBackend.System => "System",
                TTSBackend.Websocket => "Websocket",
                TTSBackend.AmazonPolly => "Amazon Polly",
                TTSBackend.Uberduck => "Uberduck",
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }

        public static bool AreLexiconsEnabled(this TTSBackend backend)
        {
            return backend switch
            {
                TTSBackend.System => true,
                TTSBackend.Websocket => false,
                TTSBackend.AmazonPolly => true,
                TTSBackend.Uberduck => false,
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }
    }
}