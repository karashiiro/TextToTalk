﻿using Newtonsoft.Json;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsVoicePreset : VoicePreset
{
    [JsonProperty("ElevenLabsVolume")] public float Volume { get; set; }

    public int PlaybackRate { get; set; }

    public string? VoiceId { get; set; }

    public override bool TrySetDefaultValues()
    {
        Volume = 1.0f;
        PlaybackRate = 100;
        VoiceId = "21m00Tcm4TlvDq8ikWAM";
        EnabledBackend = TTSBackend.ElevenLabs;
        return true;
    }
}