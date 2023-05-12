using System;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsMissingCredentialsException : Exception
{
    public ElevenLabsMissingCredentialsException(string? message) : base(message)
    {
    }
}