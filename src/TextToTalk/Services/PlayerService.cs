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
        // Deletes ALL voices for this player across all backends
        collection.DeletePlayerVoiceByPlayerId(info.Id);
    }

    public bool TryGetPlayer(string name, uint worldId, [NotNullWhen(true)] out Player? info)
    {
        return collection.TryFetchPlayerByNameAndWorld(name, worldId, out info);
    }

    // Fetch a voice preset for a specific Player + Backend combination
    public bool TryGetPlayerVoice(Player? info, [NotNullWhen(true)] out VoicePreset? voice, string backend)
    {
        voice = null;
        if (info is null) return false;

        if (collection.TryFetchPlayerVoiceByCompositeKey(info.Id, backend, out var voiceInfo))
        {
            voice = voices.FirstOrDefault(v => v.Id == voiceInfo.VoicePresetId);
        }

        return voice != null;
    }

    public bool TryGetPlayerOtherZone(string name, [NotNullWhen(true)] out Player? info)
    {
        return collection.TryFetchPlayerByName(name, out info);
    }

    // Allows setting/replacing a voice specifically for one backend
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

        string backend = voice.EnabledBackend.ToString();

        // Modified: Only check and delete for the specific backend provided
        if (TryGetPlayerVoice(info, out _, backend))
        {
            collection.DeletePlayerVoiceByCompositeKey(info.Id, backend);
        }

        // Modified: Store with the backend string to satisfy the composite requirement
        collection.StorePlayerVoice(new PlayerVoice
        {
            PlayerId = info.Id,
            VoicePresetId = voice.Id,
            VoiceBackend = backend
        });

        return true;
    }
}