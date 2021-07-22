using System;
using System.Numerics;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends
{
    public abstract class VoiceBackend : IDisposable
    {
        public Vector4 TitleBarColor { get; protected set; }

        public abstract void Say(TextSource source, Gender gender, string text);

        public abstract void CancelAllSpeech();

        public abstract void CancelSay(TextSource source);

        public abstract void DrawSettings(ImExposedFunctions helpers);

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}