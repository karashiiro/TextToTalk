using System;
using System.Collections.Generic;
using System.Linq;

namespace TextToTalk;

public class NpcService
{
    private readonly IDictionary<Guid, NpcInfo> npc;
    private readonly IDictionary<Guid, int> npcVoices;
    private readonly IList<VoicePreset> voices;

    public NpcService(IDictionary<Guid, NpcInfo> npc, IDictionary<Guid, int> npcVoices,
        IList<VoicePreset> voices)
    {
        this.npc = npc;
        this.npcVoices = npcVoices;
        this.voices = voices;
    }

    public bool AddNpc(string? name)
    {
        if (TryGetNpcByInfo(name, out _)) return false;
        var localId = Guid.NewGuid();
        var info = new NpcInfo { LocalId = localId, Name = name };
        this.npc[localId] = info;
        return true;
    }

    public void DeleteNpc(NpcInfo info)
    {
        this.npc.Remove(info.LocalId);
        this.npcVoices.Remove(info.LocalId);
    }

    public bool TryGetNpcByInfo(string? name, out NpcInfo info)
    {
        info = this.npc.Values.FirstOrDefault(info =>
            string.Equals(info.Name, name, StringComparison.InvariantCultureIgnoreCase));
        return info != null;
    }

    public bool TryGetNpcVoice(NpcInfo info, out VoicePreset voice)
    {
        voice = !this.npcVoices.TryGetValue(info.LocalId, out var voiceId)
            ? null
            : this.voices.FirstOrDefault(v => v.Id == voiceId);
        return voice != null;
    }

    public bool SetNpcVoice(NpcInfo info, VoicePreset voice)
    {
        if (!this.npc.ContainsKey(info.LocalId))
        {
            return false;
        }

        if (this.voices.All(v => v.Id != voice.Id))
        {
            return false;
        }

        this.npcVoices[info.LocalId] = voice.Id;

        return true;
    }
}