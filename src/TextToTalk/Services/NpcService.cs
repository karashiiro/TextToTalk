using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Services;

namespace TextToTalk.Services;

public class NpcService(NpcCollection collection, IList<VoicePreset> voices)
{
    public IEnumerable<Npc> GetAllNpcs() => collection.FetchAllNpcs();

    public bool AddNpc(string name)
    {
        if (TryGetNpc(name, out _)) return false;
        collection.StoreNpc(new Npc { Name = name });
        return true;
    }

    public void DeleteNpc(Npc info)
    {
        collection.DeleteNpcById(info.Id);
        // Deletes all voice presets associated with this NPC across all backends
        collection.DeleteNpcVoiceByNpcId(info.Id);
    }

    public bool TryGetNpc(string name, [NotNullWhen(true)] out Npc? info)
    {
        return collection.TryFetchNpcByName(name, out info);
    }

    // Fetch a voice preset for a specific NPC + Backend combination
    public bool TryGetNpcVoice(Npc? info, string backend, [NotNullWhen(true)] out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;

        if (collection.TryFetchNpcVoiceByCompositeKey(info.Id, backend, out var voiceInfo))
        {
            voice = voices.FirstOrDefault(v => v.Id == voiceInfo.VoicePresetId);
        }

        return voice != null;
    }

    public void UpdateNpc(Npc info) => collection.StoreNpc(info);

    // Allows setting/replacing a voice specifically for one backend
    public bool SetNpcVoice(Npc info, VoicePreset voice)
    {
        if (info.Name is null || !TryGetNpc(info.Name, out _)) return false;

        if (voices.All(v => v.Id == voice.Id)) return false;
        string backend = voice.EnabledBackend.ToString();

        if (TryGetNpcVoice(info, backend, out _))
        {
            collection.DeleteNpcVoiceByCompositeKey(info.Id, backend);
        }

        collection.StoreNpcVoice(new NpcVoice
        {
            NpcId = info.Id,
            VoicePresetId = voice.Id,
            VoiceBackend = backend
        });

        return true;
    }
}