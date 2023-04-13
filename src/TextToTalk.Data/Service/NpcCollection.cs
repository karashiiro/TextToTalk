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

    /// <summary>
    /// Fetches all stored NPCs from the database.
    /// </summary>
    /// <returns>The stored NPCs.</returns>
    public IEnumerable<Npc> FetchAllNpcs()
    {
        return GetCollection().FindAll();
    }

    /// <summary>
    /// Fetches an NPC from the database using their name and world.
    /// </summary>
    /// <param name="name">The NPC's name.</param>
    /// <param name="npc">The NPC, or null if they couldn't be found.</param>
    /// <returns>If the NPC could be found.</returns>
    public bool TryFetchNpcByName(string name, [NotNullWhen(true)] out Npc? npc)
    {
        var collection = GetCollection();
        npc = collection.Query()
            .Where(npc => npc.Name == name)
            .FirstOrDefault();
        return npc != null;
    }

    /// <summary>
    /// Stores an NPC in the database.
    /// </summary>
    /// <param name="npc">The NPC to store.</param>
    public void StoreNpc(Npc npc)
    {
        var collection = GetCollection();
        if (!collection.Update(npc.Id, npc))
        {
            collection.Insert(npc);
        }
    }

    /// <summary>
    /// Deletes an NPC from the database using their local ID.
    /// </summary>
    /// <param name="id">The NPC's ID.</param>
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