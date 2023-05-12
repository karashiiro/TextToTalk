using System.Net;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsUnauthorizedException : ElevenLabsFailedException
{
    public ElevenLabsUnauthorizedException(HttpStatusCode status, string? message) : base(status, message)
    {
    }
}