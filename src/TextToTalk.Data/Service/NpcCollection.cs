using System.Diagnostics.CodeAnalysis;
using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Service;

public class NpcCollection
{
    private const string NpcCollectionName = "npc";
    private const string NpcVoiceCollectionName = "npc_voice";

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
        return GetNpcCollection().FindAll();
    }

    /// <summary>
    /// Fetches an NPC from the database using their name and world.
    /// </summary>
    /// <param name="name">The NPC's name.</param>
    /// <param name="npc">The NPC, or null if they couldn't be found.</param>
    /// <returns>If the NPC could be found.</returns>
    public bool TryFetchNpcByName(string name, [NotNullWhen(true)] out Npc? npc)
    {
        var collection = GetNpcCollection();
        npc = collection.Query()
            .Where(npc => npc.Name == name)
            .FirstOrDefault();
        return npc != null;
    }

    /// <summary>
    /// Fetches an NPC voice from the database using their local ID.
    /// </summary>
    /// <param name="id">The NPC's local ID.</param>
    /// <param name="voice">The voice info, or null if it couldn't be found.</param>
    /// <returns>If the voice could be found.</returns>
    public bool TryFetchNpcVoiceByNpcId(Guid id, [NotNullWhen(true)] out NpcVoice? voice)
    {
        var collection = GetNpcVoiceCollection();
        voice = collection.Query()
            .Where(v => v.NpcId == id)
            .FirstOrDefault();
        return voice != null;
    }

    /// <summary>
    /// Stores an NPC in the database.
    /// </summary>
    /// <param name="npc">The NPC to store.</param>
    public void StoreNpc(Npc npc)
    {
        var collection = GetNpcCollection();
        if (!collection.Update(npc.Id, npc))
        {
            collection.Insert(npc);
        }
    }

    /// <summary>
    /// Stores an NPC voice in the database.
    /// </summary>
    /// <param name="voice">The NPC voice to store.</param>
    public void StoreNpcVoice(NpcVoice voice)
    {
        var collection = GetNpcVoiceCollection();
        if (!collection.Update(voice.Id, voice))
        {
            collection.Insert(voice);
        }
    }

    /// <summary>
    /// Deletes an NPC from the database using their local ID.
    /// </summary>
    /// <param name="id">The NPC's ID.</param>
    public void DeleteNpcById(Guid id)
    {
        var collection = GetNpcCollection();
        collection.Delete(id);
    }

    /// <summary>
    /// Deletes an NPC voice from the database using their local ID.
    /// </summary>
    /// <param name="id">The NPC's ID.</param>
    public void DeleteNpcVoiceByNpcId(Guid id)
    {
        var collection = GetNpcVoiceCollection();
        collection.DeleteMany(v => v.NpcId == id);
    }

    private ILiteCollection<Npc> GetNpcCollection()
    {
        var collection = this.db.GetCollection<Npc>(NpcCollectionName);
        EnsureIndices(collection);
        return collection;
    }

    private ILiteCollection<NpcVoice> GetNpcVoiceCollection()
    {
        var collection = this.db.GetCollection<NpcVoice>(NpcVoiceCollectionName);
        EnsureIndices(collection);
        return collection;
    }

    private static void EnsureIndices(ILiteCollection<Npc> collection)
    {
        // "By default, an index over _id is created upon the first insertion."
        // https://www.litedb.org/docs/indexes/
        collection.EnsureIndex(npc => npc.Name);
    }

    private static void EnsureIndices(ILiteCollection<NpcVoice> collection)
    {
        collection.EnsureIndex(v => v.NpcId);
    }
}