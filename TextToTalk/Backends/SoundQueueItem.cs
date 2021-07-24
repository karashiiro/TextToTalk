using System;

namespace TextToTalk.Backends
{
    public class SoundQueueItem : IDisposable
    {
        public TextSource Source { get; set; }

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}