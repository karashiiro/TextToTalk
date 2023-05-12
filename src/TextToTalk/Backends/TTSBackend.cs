﻿using System;

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
                TTSBackend.Azure => "Azure Cognitive Services",
                TTSBackend.ElevenLabs => "ElevenLabs",
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
                TTSBackend.Azure => true,
                TTSBackend.ElevenLabs => false,
                _ => throw new ArgumentOutOfRangeException(nameof(backend)),
            };
        }
    }
}