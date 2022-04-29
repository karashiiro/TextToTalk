using System;

namespace TextToTalk.Backends.Uberduck;

public class UberduckMissingCredentialsException : Exception
{
    public UberduckMissingCredentialsException(string message) : base(message)
    {
    }
}