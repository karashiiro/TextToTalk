using System.Diagnostics.CodeAnalysis;
using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Service;

public class NpcCollection
{
    private const string CollectionName = "npc";

    private readonly ILiteDatabase db;

    public NpcCollection(ILiteDatabase db)
    {
        this.db = db;
    }

    public IEnumerable<Npc> GetAllNpcs()
    {
        return GetCollection().FindAll();
    }

    public bool TryFetchNpcByName(string name, [NotNullWhen(true)] out Npc? npc)
    {
        var collection = GetCollection();
        npc = collection.Query()
            .Where(npc => npc.Name == name)
            .FirstOrDefault();
        return npc != null;
    }

    public void StoreNpc(Npc npc)
    {
        var collection = GetCollection();
        if (!collection.Update(npc.Id, npc))
        {
            collection.Insert(npc);
        }
    }

    public void DeleteNpcById(Guid id)
    {
        var collection = GetCollection();
        collection.Delete(id);
    }

    private ILiteCollection<Npc> GetCollection()
    {
        var collection = this.db.GetCollection<Npc>(CollectionName);
        EnsureIndices(collection);
        return collection;
    }

    private static void EnsureIndices(ILiteCollection<Npc> collection)
    {
        // "By default, an index over _id is created upon the first insertion."
        // https://www.litedb.org/docs/indexes/
        collection.EnsureIndex(p => p.Name);
    }
}