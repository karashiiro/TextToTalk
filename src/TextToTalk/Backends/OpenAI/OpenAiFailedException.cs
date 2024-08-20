using System;
using System.Net;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiFailedException(HttpStatusCode status, OpenAiErrorResponse.Inner? errorResponse, string? message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = status;
    public OpenAiErrorResponse.Inner? Error { get; } = errorResponse;
}