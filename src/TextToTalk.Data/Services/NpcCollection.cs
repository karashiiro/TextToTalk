using System.Diagnostics.CodeAnalysis;
using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Services;

public class NpcCollection(ILiteDatabase db)
{
    private const string NpcCollectionName = "npc";
    private const string NpcVoiceCollectionName = "npc_voice";

    public IEnumerable<Npc> FetchAllNpcs() => GetNpcCollection().FindAll();

    public bool TryFetchNpcByName(string name, [NotNullWhen(true)] out Npc? npc)
    {
        var collection = GetNpcCollection();
        npc = collection.Query()
            .Where(npc => npc.Name == name)
            .FirstOrDefault();
        return npc != null;
    }

    /// <summary>
    /// Fetches a specific voice for an NPC and a specific backend.
    /// </summary>
    public bool TryFetchNpcVoiceByCompositeKey(Guid npcId, string backend, [NotNullWhen(true)] out NpcVoice? voice)
    {
        var collection = GetNpcVoiceCollection();
        voice = collection.Query()
            .Where(v => v.NpcId == npcId && v.VoiceBackend == backend)
            .FirstOrDefault();
        return voice != null;
    }

    public void StoreNpc(Npc npc)
    {
        var collection = GetNpcCollection();
        if (!collection.Update(npc.Id, npc))
        {
            collection.Insert(npc);
        }
    }

    public void StoreNpcVoice(NpcVoice voice)
    {
        var collection = GetNpcVoiceCollection();
        if (!collection.Update(voice.Id, voice))
        {
            collection.Insert(voice);
        }
    }

    public void DeleteNpcById(Guid id) => GetNpcCollection().Delete(id);

    /// <summary>
    /// Deletes ALL voice presets for a specific NPC (e.g., when the NPC is deleted).
    /// </summary>
    public void DeleteNpcVoiceByNpcId(Guid id)
    {
        var collection = GetNpcVoiceCollection();
        collection.DeleteMany(v => v.NpcId == id);
    }

    /// <summary>
    /// Deletes a specific voice preset for one NPC on a specific backend.
    /// </summary>
    public void DeleteNpcVoiceByCompositeKey(Guid npcId, string backend)
    {
        var collection = GetNpcVoiceCollection();
        collection.DeleteMany(v => v.NpcId == npcId && v.VoiceBackend == backend);
    }

    private ILiteCollection<Npc> GetNpcCollection()
    {
        var collection = db.GetCollection<Npc>(NpcCollectionName);
        collection.EnsureIndex(npc => npc.Name);
        return collection;
    }

    private ILiteCollection<NpcVoice> GetNpcVoiceCollection()
    {
        var collection = db.GetCollection<NpcVoice>(NpcVoiceCollectionName);
        // Added index for the backend to speed up composite queries
        collection.EnsureIndex(v => v.NpcId);
        collection.EnsureIndex(v => v.VoiceBackend);
        return collection;
    }
}