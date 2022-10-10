using System;
using System.Collections.Generic;
using System.Linq;

namespace TextToTalk;

public class PlayerService
{
    private readonly IDictionary<Guid, PlayerInfo> players;
    private readonly IDictionary<Guid, int> playerVoices;
    private readonly IList<VoicePreset> voices;

    public PlayerService(IDictionary<Guid, PlayerInfo> players, IDictionary<Guid, int> playerVoices,
        IList<VoicePreset> voices)
    {
        this.players = players;
        this.playerVoices = playerVoices;
        this.voices = voices;
    }

    public void AddPlayer(string name, uint worldId)
    {
        var localId = Guid.NewGuid();
        var info = new PlayerInfo { LocalId = localId, Name = name, WorldId = worldId };
        this.players[localId] = info;
    }

    public bool TryGetPlayerByInfo(string name, uint worldId, out PlayerInfo info)
    {
        info = this.players.Values.FirstOrDefault(info => info.Name == name && info.WorldId == worldId);
        return info != null;
    }

    public bool TryGetPlayerVoice(PlayerInfo info, out VoicePreset voice)
    {
        voice = !this.playerVoices.TryGetValue(info.LocalId, out var voiceId)
            ? null
            : this.voices.FirstOrDefault(v => v.Id == voiceId);
        return voice != null;
    }

    public bool SetPlayerVoice(PlayerInfo info, VoicePreset voice)
    {
        if (!this.players.ContainsKey(info.LocalId))
        {
            return false;
        }

        if (this.voices.All(v => v.Id != voice.Id))
        {
            return false;
        }

        this.playerVoices[info.LocalId] = voice.Id;

        return true;
    }
}