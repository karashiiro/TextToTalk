using System;

namespace TextToTalk.Exceptions;

public class ServiceException : Exception
{
    public ServiceException(string message) : base(message) { }
}