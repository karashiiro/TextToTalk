using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Services;

namespace TextToTalk.Services;

public class PlayerService(PlayerCollection collection, IList<VoicePreset> voices)
{
    public IEnumerable<Player> GetAllPlayers()
    {
        return collection.FetchAllPlayers();
    }

    public void UpdatePlayer(Player player)
    {
        collection.StorePlayer(player);
    }

    public bool AddPlayer(string name, uint worldId)
    {
        if (TryGetPlayer(name, worldId, out _)) return false;
        var info = new Player { Name = name, WorldId = worldId };
        collection.StorePlayer(info);
        return true;
    }

    public void DeletePlayer(Player info)
    {
        collection.DeletePlayerById(info.Id);
        collection.DeletePlayerVoiceByPlayerId(info.Id);
    }

    public bool TryGetPlayer(string name, uint worldId, [NotNullWhen(true)] out Player? info)
    {
        return collection.TryFetchPlayerByNameAndWorld(name, worldId, out info);
    }

    public bool TryGetPlayerVoice(Player? info, [NotNullWhen(true)] out VoicePreset? voice)
    {
        voice = null;
        if (info is null) return false;
        if (collection.TryFetchPlayerVoiceByPlayerId(info.Id, out var voiceInfo))
        {
            voice = voices.FirstOrDefault(v => v.Id == voiceInfo.VoicePresetId);
        }

        return voice != null;
    }

    public bool TryGetPlayerOtherZone(string name, [NotNullWhen(true)] out Player? info)
    {
        return collection.TryFetchPlayerByName(name, out info);
    }

    public bool SetPlayerVoice(Player info, VoicePreset voice)
    {
        if (info.Name is null || !TryGetPlayer(info.Name, info.WorldId, out _))
        {
            return false;
        }

        if (voices.All(v => v.Id != voice.Id))
        {
            return false;
        }

        if (TryGetPlayerVoice(info, out _))
        {
            collection.DeletePlayerVoiceByPlayerId(info.Id);
        }

        collection.StorePlayerVoice(new PlayerVoice { PlayerId = info.Id, VoicePresetId = voice.Id });

        return true;
    }
}