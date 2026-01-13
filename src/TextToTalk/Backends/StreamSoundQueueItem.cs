using System.IO;
using System.Net.Http;
using System.Diagnostics;
using static TextToTalk.Backends.System.SystemSoundQueue;

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
                try
                {
                    Data.Dispose();
                }
                catch { }

                base.Dispose(disposing);
            }
        }

        public class StreamingSoundQueueItem : SoundQueueItem
        {
            public Stream Data { get; init; }

            public float Volume { get; init; }

            public StreamFormat Format { get; init; }
            public HttpResponseMessage? Response { get; set; }
            public bool Aborted { get; set; }

            public long? StartTime { get; set; } // Use GetTimestamp() value

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try
                    {
                        Data.Dispose();
                    }
                    catch { }

                    base.Dispose(disposing);
                }                                           
            }
        }
    }
}