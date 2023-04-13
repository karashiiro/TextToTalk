using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Service;

namespace TextToTalk;

public class NpcService
{
    private readonly NpcCollection npc;
    private readonly IList<VoicePreset> voices;

    public NpcService(NpcCollection npc, IList<VoicePreset> voices)
    {
        this.npc = npc;
        this.voices = voices;
    }

    public IEnumerable<Npc> GetAllNpcs()
    {
        return this.npc.FetchAllNpcs();
    }

    public bool AddNpc(string name)
    {
        if (TryGetNpc(name, out _)) return false;
        var info = new Npc { Name = name };
        this.npc.StoreNpc(info);
        return true;
    }

    public void DeleteNpc(Npc info)
    {
        this.npc.DeleteNpcById(info.Id);
        this.npc.DeleteNpcVoiceByNpcId(info.Id);
    }

    public bool TryGetNpc(string name, [NotNullWhen(true)] out Npc? info)
    {
        return this.npc.TryFetchNpcByName(name, out info);
    }

    public bool TryGetNpcVoice(Npc? info, [NotNullWhen(true)] out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;
        if (this.npc.TryFetchNpcVoiceByNpcId(info.Id, out var voiceInfo))
        {
            voice = this.voices.FirstOrDefault(v => v.Id == voiceInfo.VoicePresetId);
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

        if (TryGetNpcVoice(info, out _))
        {
            this.npc.DeleteNpcVoiceByNpcId(info.Id);
        }

        this.npc.StoreNpcVoice(new NpcVoice { NpcId = info.Id, VoicePresetId = voice.Id });

        return true;
    }
}