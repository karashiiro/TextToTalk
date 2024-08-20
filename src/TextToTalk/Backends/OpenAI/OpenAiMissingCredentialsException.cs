using System;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiMissingCredentialsException(string? message) : Exception(message);