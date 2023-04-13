using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Service;

namespace TextToTalk;

public class PlayerService
{
    private readonly PlayerCollection players;
    private readonly IDictionary<Guid, int> playerVoices;
    private readonly IList<VoicePreset> voices;

    public PlayerService(
        PlayerCollection players,
        IDictionary<Guid, int> playerVoices,
        IList<VoicePreset> voices)
    {
        this.players = players;
        this.playerVoices = playerVoices;
        this.voices = voices;
    }

    public IEnumerable<Player> GetAllPlayers()
    {
        return this.players.GetAllPlayers();
    }

    public void UpdatePlayer(Player player)
    {
        this.players.StorePlayer(player);
    }

    public bool AddPlayer(string name, uint worldId)
    {
        if (TryGetPlayer(name, worldId, out _)) return false;
        var localId = Guid.NewGuid();
        var info = new Player { Id = localId, Name = name, WorldId = worldId };
        this.players.StorePlayer(info);
        return true;
    }

    public void DeletePlayer(Player info)
    {
        this.players.DeletePlayerById(info.Id);
        this.playerVoices.Remove(info.Id);
    }

    public bool TryGetPlayer(string name, uint worldId, [NotNullWhen(true)] out Player? info)
    {
        return this.players.TryFetchPlayerByNameAndWorld(name, worldId, out info);
    }

    public bool TryGetPlayerVoice(Player? info, out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;
        if (this.playerVoices.TryGetValue(info.Id, out var voiceId))
        {
            voice = this.voices.FirstOrDefault(v => v.Id == voiceId);
        }

        return voice != null;
    }

    public bool SetPlayerVoice(Player info, VoicePreset voice)
    {
        if (info.Name is null || !TryGetPlayer(info.Name, info.WorldId, out _))
        {
            return false;
        }

        if (this.voices.All(v => v.Id != voice.Id))
        {
            return false;
        }

        this.playerVoices[info.Id] = voice.Id;

        return true;
    }
}