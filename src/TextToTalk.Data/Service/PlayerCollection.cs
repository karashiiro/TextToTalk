using System.Diagnostics.CodeAnalysis;
using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Service;

public class PlayerCollection
{
    private const string PlayerCollectionName = "player";
    private const string PlayerVoiceCollectionName = "player_voice";

    private readonly ILiteDatabase db;

    public PlayerCollection(ILiteDatabase db)
    {
        this.db = db;
    }

    /// <summary>
    /// Fetches all stored players from the database.
    /// </summary>
    /// <returns>The stored players.</returns>
    public IEnumerable<Player> FetchAllPlayers()
    {
        return GetPlayerCollection().FindAll();
    }

    /// <summary>
    /// Fetches a player from the database using their name and world.
    /// </summary>
    /// <param name="name">The player's name.</param>
    /// <param name="worldId">The player's world ID.</param>
    /// <param name="player">The player, or null if they couldn't be found.</param>
    /// <returns>If the player could be found.</returns>
    public bool TryFetchPlayerByNameAndWorld(string name, uint worldId, [NotNullWhen(true)] out Player? player)
    {
        var collection = GetPlayerCollection();
        player = collection.Query()
            .Where(p => p.Name == name && p.WorldId == worldId)
            .FirstOrDefault();
        return player != null;
    }

    /// <summary>
    /// Fetches a player voice from the database using their local ID.
    /// </summary>
    /// <param name="id">The player's local ID.</param>
    /// <param name="voice">The voice info, or null if it couldn't be found.</param>
    /// <returns>If the voice could be found.</returns>
    public bool TryFetchPlayerVoiceByPlayerId(Guid id, [NotNullWhen(true)] out PlayerVoice? voice)
    {
        var collection = GetPlayerVoiceCollection();
        voice = collection.Query()
            .Where(v => v.PlayerId == id)
            .FirstOrDefault();
        return voice != null;
    }

    /// <summary>
    /// Stores a player in the database.
    /// </summary>
    /// <param name="player">The player to store.</param>
    public void StorePlayer(Player player)
    {
        var collection = GetPlayerCollection();
        if (!collection.Update(player.Id, player))
        {
            collection.Insert(player);
        }
    }

    /// <summary>
    /// Stores a player voice in the database.
    /// </summary>
    /// <param name="voice">The player voice to store.</param>
    public void StorePlayerVoice(PlayerVoice voice)
    {
        var collection = GetPlayerVoiceCollection();
        if (!collection.Update(voice.Id, voice))
        {
            collection.Insert(voice);
        }
    }

    /// <summary>
    /// Deletes a player from the database using their local ID.
    /// </summary>
    /// <param name="id">The player's ID.</param>
    public void DeletePlayerById(Guid id)
    {
        var collection = GetPlayerCollection();
        collection.Delete(id);
    }

    /// <summary>
    /// Deletes a player voice from the database using their local ID.
    /// </summary>
    /// <param name="id">The player's ID.</param>
    public void DeletePlayerVoiceByPlayerId(Guid id)
    {
        var collection = GetPlayerVoiceCollection();
        collection.DeleteMany(v => v.PlayerId == id);
    }

    private ILiteCollection<Player> GetPlayerCollection()
    {
        var collection = this.db.GetCollection<Player>(PlayerCollectionName);
        EnsureIndices(collection);
        return collection;
    }

    private ILiteCollection<PlayerVoice> GetPlayerVoiceCollection()
    {
        var collection = this.db.GetCollection<PlayerVoice>(PlayerVoiceCollectionName);
        EnsureIndices(collection);
        return collection;
    }

    private static void EnsureIndices(ILiteCollection<Player> collection)
    {
        // "By default, an index over _id is created upon the first insertion."
        // https://www.litedb.org/docs/indexes/
        collection.EnsureIndex(p => p.Name);
        collection.EnsureIndex(p => p.WorldId);
    }

    private static void EnsureIndices(ILiteCollection<PlayerVoice> collection)
    {
        collection.EnsureIndex(v => v.PlayerId);
    }
}