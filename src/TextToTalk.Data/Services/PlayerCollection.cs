using System.Diagnostics.CodeAnalysis;
using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Services;

public class PlayerCollection(ILiteDatabase db)
{
    private const string PlayerCollectionName = "player";
    private const string PlayerVoiceCollectionName = "player_voice";

    public IEnumerable<Player> FetchAllPlayers() => GetPlayerCollection().FindAll();

    public bool TryFetchPlayerByNameAndWorld(string name, uint worldId, [NotNullWhen(true)] out Player? player)
    {
        var collection = GetPlayerCollection();
        player = collection.Query()
            .Where(p => p.Name == name && p.WorldId == worldId)
            .FirstOrDefault();
        return player != null;
    }

    public bool TryFetchPlayerByName(string name,[NotNullWhen(true)] out Player? player)
    {
        var collection = GetPlayerCollection();
        player = collection.Query()
            .Where(p => p.Name == name)
            .FirstOrDefault();
        return player != null;
    }

    /// <summary>
    /// Fetches a player voice using the Player's Guid and the specific backend name.
    /// </summary>
    public bool TryFetchPlayerVoiceByCompositeKey(Guid playerId, string backend, [NotNullWhen(true)] out PlayerVoice? voice)
    {
        var collection = GetPlayerVoiceCollection();
        voice = collection.Query()
            .Where(v => v.PlayerId == playerId && v.VoiceBackend == backend)
            .FirstOrDefault();
        return voice != null;
    }

    public void StorePlayer(Player player)
    {
        var collection = GetPlayerCollection();
        if (!collection.Update(player.Id, player))
        {
            collection.Insert(player);
        }
    }

    public void StorePlayerVoice(PlayerVoice voice)
    {
        var collection = GetPlayerVoiceCollection();
        if (!collection.Update(voice.Id, voice))
        {
            collection.Insert(voice);
        }
    }

    public void DeletePlayerById(Guid id) => GetPlayerCollection().Delete(id);

    /// <summary>
    /// Deletes all voices associated with a player (Cleanup).
    /// </summary>
    public void DeletePlayerVoiceByPlayerId(Guid id)
    {
        var collection = GetPlayerVoiceCollection();
        collection.DeleteMany(v => v.PlayerId == id);
    }

    /// <summary>
    /// Deletes the specific voice preset for a player on a specific backend.
    /// </summary>
    public void DeletePlayerVoiceByCompositeKey(Guid playerId, string backend)
    {
        var collection = GetPlayerVoiceCollection();
        // FIXED: Changed v.Id to v.PlayerId to correctly target the relationship
        collection.DeleteMany(v => v.PlayerId == playerId && v.VoiceBackend == backend);
    }

    private ILiteCollection<Player> GetPlayerCollection()
    {
        var collection = db.GetCollection<Player>(PlayerCollectionName);
        collection.EnsureIndex(p => p.Name);
        collection.EnsureIndex(p => p.WorldId);
        return collection;
    }

    private ILiteCollection<PlayerVoice> GetPlayerVoiceCollection()
    {
        var collection = db.GetCollection<PlayerVoice>(PlayerVoiceCollectionName);
        // Added index for the backend to speed up composite queries
        collection.EnsureIndex(v => v.PlayerId);
        collection.EnsureIndex(v => v.VoiceBackend);
        return collection;
    }
}