using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Services;

namespace TextToTalk.Services;

public class NpcService(NpcCollection collection, IList<VoicePreset> voices)
{
    public IEnumerable<Npc> GetAllNpcs()
    {
        return collection.FetchAllNpcs();
    }

    public bool AddNpc(string name)
    {
        if (TryGetNpc(name, out _)) return false;
        var info = new Npc { Name = name };
        collection.StoreNpc(info);
        return true;
    }

    public void DeleteNpc(Npc info)
    {
        collection.DeleteNpcById(info.Id);
        collection.DeleteNpcVoiceByNpcId(info.Id);
    }

    public bool TryGetNpc(string name, [NotNullWhen(true)] out Npc? info)
    {
        return collection.TryFetchNpcByName(name, out info);
    }

    public bool TryGetNpcVoice(Npc? info, [NotNullWhen(true)] out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;
        if (collection.TryFetchNpcVoiceByNpcId(info.Id, out var voiceInfo))
        {
            voice = voices.FirstOrDefault(v => v.Id == voiceInfo.VoicePresetId);
        }

        return voice != null;
    }

    public void UpdateNpc(Npc info)
    {
        collection.StoreNpc(info);
    }

    public bool SetNpcVoice(Npc info, VoicePreset voice)
    {
        if (info.Name is null || !TryGetNpc(info.Name, out _))
        {
            return false;
        }

        if (voices.All(v => v.Id != voice.Id))
        {
            return false;
        }

        if (TryGetNpcVoice(info, out _))
        {
            collection.DeleteNpcVoiceByNpcId(info.Id);
        }

        collection.StoreNpcVoice(new NpcVoice { NpcId = info.Id, VoicePresetId = voice.Id });

        return true;
    }
}