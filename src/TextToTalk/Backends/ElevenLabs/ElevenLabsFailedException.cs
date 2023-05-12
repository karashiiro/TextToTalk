using System;
using System.Net;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsFailedException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ElevenLabsFailedException(HttpStatusCode status, string? message) : base(message)
    {
        StatusCode = status;
    }
}