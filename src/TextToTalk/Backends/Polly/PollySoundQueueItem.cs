using System.IO;

namespace TextToTalk.Backends.Polly
{
    public class PollySoundQueueItem : SoundQueueItem
    {
        public MemoryStream Data { get; set; }

        public float Volume { get; set; }

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