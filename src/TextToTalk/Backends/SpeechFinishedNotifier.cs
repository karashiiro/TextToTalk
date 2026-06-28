using System;

namespace TextToTalk.Backends
{
    public static class SpeechFinishedNotifier
    {
        public static event Action<TextSource, string?>? SpeechFinished;

        public static void Trigger(TextSource source, string? text)
        {
            SpeechFinished?.Invoke(source, text);
        }
    }
}
