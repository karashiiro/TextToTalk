using System;
using TextToTalk.Backends.Kokoro;

namespace TextToTalk.Backends
{
    public enum TTSBackend : long
    {
        System,
        Websocket,
        AmazonPolly,
        Uberduck,
        Azure,
        ElevenLabs,
        OpenAi,
        GoogleCloud,
        Kokoro,
        Megaphone,
    }

    public static class TTSBackendExtensions
    {
        public static string GetFormattedName(this TTSBackend backend, PluginConfiguration? config = null)
        {
            return backend switch
            {
                TTSBackend.System => "System",
                TTSBackend.Websocket => "Websocket",
                TTSBackend.AmazonPolly => "Amazon Polly",
                TTSBackend.Uberduck => "Uberduck",
                TTSBackend.Azure => "Azure Cognitive Services",
                TTSBackend.ElevenLabs => "ElevenLabs",
                TTSBackend.OpenAi => "OpenAI",
                TTSBackend.GoogleCloud => "Google Cloud",
                TTSBackend.Kokoro when config != null && KokoroBackend.IsModelFileDownloaded(config) => "Kokoro",
                TTSBackend.Kokoro => "Kokoro (169MB download required)",
                TTSBackend.Megaphone => "Megaphone",
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }

        public static int GetDisplayOrder(this TTSBackend backend) => backend switch
        {
            TTSBackend.Megaphone => 0,
            TTSBackend.System => 1,
            TTSBackend.Websocket => 2,
            TTSBackend.AmazonPolly => 3,
            TTSBackend.Azure => 4,
            TTSBackend.ElevenLabs => 5,
            TTSBackend.OpenAi => 6,
            TTSBackend.GoogleCloud => 7,
            TTSBackend.Kokoro => 8,
            TTSBackend.Uberduck => 9,
            _ => int.MaxValue,
        };

        public static bool IsRelayBackend(this TTSBackend backend) =>
            backend is TTSBackend.Websocket or TTSBackend.Megaphone;

        public static bool AreLexiconsEnabled(this TTSBackend backend)
        {
            return backend switch
            {
                TTSBackend.System => true,
                TTSBackend.Websocket => false,
                TTSBackend.AmazonPolly => true,
                TTSBackend.Uberduck => false,
                TTSBackend.Azure => true,
                TTSBackend.ElevenLabs => false,
                TTSBackend.OpenAi => false,
                TTSBackend.GoogleCloud => false,
                TTSBackend.Kokoro => false,
                TTSBackend.Megaphone => false,
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }
    }
}