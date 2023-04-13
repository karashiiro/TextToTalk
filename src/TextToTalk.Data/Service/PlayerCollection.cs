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

    public IEnumerable<Player> GetAllPlayers()
    {
        return GetCollection().FindAll();
    }

    public bool TryFetchPlayerByNameAndWorld(string name, uint worldId, [NotNullWhen(true)] out Player? player)
    {
        var collection = GetCollection();
        player = collection.Query()
            .Where(p => p.Name == name && p.WorldId == worldId)
            .FirstOrDefault();
        return player != null;
    }

    public void StorePlayer(Player player)
    {
        var collection = GetCollection();
        if (!collection.Update(player.Id, player))
        {
            collection.Insert(player);
        }
    }

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