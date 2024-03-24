using System;
using System.Numerics;

namespace TextToTalk.Backends
{
    public abstract class VoiceBackend : IDisposable
    {
        public Vector4 TitleBarColor { get; protected init; }

        public abstract void Say(SayRequest request);

        public abstract void CancelAllSpeech();

        public abstract void CancelSay(TextSource source);

        public abstract void DrawSettings(IConfigUIDelegates helpers);

        public abstract TextSource GetCurrentlySpokenTextSource();

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}