using System;

namespace TextToTalk.Backends.Uberduck;

public class UberduckUnauthorizedException : Exception
{
    public UberduckUnauthorizedException(string message) : base(message)
    {
    }
}