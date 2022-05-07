using System;
using System.Net;

namespace TextToTalk.Backends.Uberduck;

public class UberduckFailedException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public UberduckFailedException(HttpStatusCode status, string message) : base(message)
    {
        StatusCode = status;
    }
}