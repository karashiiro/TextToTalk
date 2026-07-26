using System;
using System.Net;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioFailedException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public FishAudioFailedException(HttpStatusCode status, string? message) : base(message)
    {
        StatusCode = status;
    }
}
