using System;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioMissingCredentialsException : Exception
{
    public FishAudioMissingCredentialsException(string? message) : base(message)
    {
    }
}
