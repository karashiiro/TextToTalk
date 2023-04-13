using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Service;

namespace TextToTalk;

public class PlayerService
{
    private readonly PlayerCollection players;
    private readonly IList<VoicePreset> voices;

    public PlayerService(PlayerCollection players, IList<VoicePreset> voices)
    {
        this.players = players;
        this.voices = voices;
    }

    public IEnumerable<Player> GetAllPlayers()
    {
        return this.players.FetchAllPlayers();
    }

    public void UpdatePlayer(Player player)
    {
        this.players.StorePlayer(player);
    }

    public bool AddPlayer(string name, uint worldId)
    {
        if (TryGetPlayer(name, worldId, out _)) return false;
        var info = new Player { Name = name, WorldId = worldId };
        this.players.StorePlayer(info);
        return true;
    }

    public void DeletePlayer(Player info)
    {
        this.players.DeletePlayerById(info.Id);
        this.players.DeletePlayerVoiceByPlayerId(info.Id);
    }

    public bool TryGetPlayer(string name, uint worldId, [NotNullWhen(true)] out Player? info)
    {
        return this.players.TryFetchPlayerByNameAndWorld(name, worldId, out info);
    }

    public bool TryGetPlayerVoice(Player? info, [NotNullWhen(true)] out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;
        if (this.players.TryFetchPlayerVoiceByPlayerId(info.Id, out var voiceInfo))
        {
            voice = this.voices.FirstOrDefault(v => v.Id == voiceInfo.VoicePresetId);
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

        if (TryGetPlayerVoice(info, out _))
        {
            this.players.DeletePlayerVoiceByPlayerId(info.Id);
        }

        this.players.StorePlayerVoice(new PlayerVoice { PlayerId = info.Id, VoicePresetId = voice.Id });

        return true;
    }
}