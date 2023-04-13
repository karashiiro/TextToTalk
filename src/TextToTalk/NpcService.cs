using System;
using System.Collections.Generic;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Service;

namespace TextToTalk;

public class NpcService
{
    private readonly NpcCollection npc;
    private readonly IDictionary<Guid, int> npcVoices;
    private readonly IList<VoicePreset> voices;

    public NpcService(NpcCollection npc, IDictionary<Guid, int> npcVoices,
        IList<VoicePreset> voices)
    {
        this.npc = npc;
        this.npcVoices = npcVoices;
        this.voices = voices;
    }

    public IEnumerable<Npc> GetAllNpcs()
    {
        return this.npc.GetAllNpcs();
    }

    public bool AddNpc(string name)
    {
        if (TryGetNpc(name, out _)) return false;
        var localId = Guid.NewGuid();
        var info = new Npc { Id = localId, Name = name };
        this.npc.StoreNpc(info);
        return true;
    }

    public void DeleteNpc(Npc info)
    {
        this.npc.DeleteNpcById(info.Id);
        this.npcVoices.Remove(info.Id);
    }

    public bool TryGetNpc(string name, out Npc? info)
    {
        info = this.npc.FetchNpcByName(name);
        return info != null;
    }

    public bool TryGetNpcVoice(Npc? info, out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;
        if (this.npcVoices.TryGetValue(info.Id, out var voiceId))
        {
            voice = this.voices.FirstOrDefault(v => v.Id == voiceId);
        }

        return voice != null;
    }

    public bool SetNpcVoice(Npc info, VoicePreset voice)
    {
        if (info.Name is null || !TryGetNpc(info.Name, out _))
        {
            return false;
        }

        if (this.voices.All(v => v.Id != voice.Id))
        {
            return false;
        }

        this.npcVoices[info.Id] = voice.Id;

        return true;
    }
}