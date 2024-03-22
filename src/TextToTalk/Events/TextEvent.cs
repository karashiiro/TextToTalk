using System;
using TextToTalk.Data.Model;

namespace TextToTalk.Events;

public abstract class TextEvent
{
    public TextEventLogEntry ToLogEntry()
    {
        return new TextEventLogEntry
        {
            Event = this,
            Timestamp = DateTime.UtcNow,
        };
    }
}