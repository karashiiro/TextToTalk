using System.Diagnostics.CodeAnalysis;
using LiteDB;
using TextToTalk.Data.Model;

namespace TextToTalk.Data.Service;

public class PlayerCollection
{
    private const string CollectionName = "player";

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
        return GetCollection().FindAll();
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
        var collection = GetCollection();
        player = collection.Query()
            .Where(p => p.Name == name && p.WorldId == worldId)
            .FirstOrDefault();
        return player != null;
    }

    /// <summary>
    /// Stores a player in the database.
    /// </summary>
    /// <param name="player">The player to store.</param>
    public void StorePlayer(Player player)
    {
        var collection = GetCollection();
        if (!collection.Update(player.Id, player))
        {
            collection.Insert(player);
        }
    }

    /// <summary>
    /// Deletes a player from the database using their local ID.
    /// </summary>
    /// <param name="id">The player's ID.</param>
    public void DeletePlayerById(Guid id)
    {
        var collection = GetCollection();
        collection.Delete(id);
    }

    private ILiteCollection<Player> GetCollection()
    {
        var collection = this.db.GetCollection<Player>(CollectionName);
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
}