using System.Net;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioUnauthorizedException : FishAudioFailedException
{
    public FishAudioUnauthorizedException(HttpStatusCode status, string? message) : base(status, message)
    {
    }
}
