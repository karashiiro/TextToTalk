using System;

namespace TextToTalk.Backends
{
    public enum TTSBackend
    {
        System,
        Websocket,
        AmazonPolly,
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
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }
    }
}