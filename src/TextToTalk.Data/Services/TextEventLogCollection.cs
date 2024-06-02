using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Services;

public class TextEventLogCollection(ILiteDatabase db)
{
    private const string TextEventLogCollectionName = "event";

    public void StoreEvent(TextEventLogEntry entry)
    {
        var collection = GetTextEventLogCollection();
        collection.Insert(entry);
    }

    private ILiteCollection<TextEventLogEntry> GetTextEventLogCollection()
    {
        var collection = db.GetCollection<TextEventLogEntry>(TextEventLogCollectionName);
        EnsureIndices(collection);
        return collection;
    }

    private static void EnsureIndices(ILiteCollection<TextEventLogEntry> collection)
    {
        collection.EnsureIndex(p => p.Timestamp);
    }
}