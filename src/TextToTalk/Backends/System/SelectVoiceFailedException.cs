using System;

namespace TextToTalk.Backends.System;

public class SelectVoiceFailedException : Exception
{
    public string VoiceId { get; }

    public SelectVoiceFailedException(string voiceId, string message, Exception innerException) : base(message, innerException)
    {
        VoiceId = voiceId;
    }
}