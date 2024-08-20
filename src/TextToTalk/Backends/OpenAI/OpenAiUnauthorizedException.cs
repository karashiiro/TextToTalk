using System.Net;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiUnauthorizedException(HttpStatusCode status, string? message)
    : OpenAiFailedException(status, null, message);