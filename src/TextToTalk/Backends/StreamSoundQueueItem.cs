using System.IO;

namespace TextToTalk.Backends
{
    public class StreamSoundQueueItem : SoundQueueItem
    {
        public MemoryStream Data { get; init; }

        public float Volume { get; init; }

        public StreamFormat Format { get; init; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Data.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}