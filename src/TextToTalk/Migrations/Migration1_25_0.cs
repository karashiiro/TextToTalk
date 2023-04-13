using System;
using System.Collections.Generic;
using System.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Service;
#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_25_0 : IConfigurationMigration
{
    private readonly PlayerCollection playerCollection;

    public Migration1_25_0(PlayerCollection playerCollection)
    {
        this.playerCollection = playerCollection;
    }
    
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return config.Players?.Any() == true;
    }

    public void Migrate(PluginConfiguration config)
    {
        foreach (var (_, playerInfo) in config.Players)
        {
            this.playerCollection.StorePlayer(new Player
            {
                Id = playerInfo.LocalId,
                Name = playerInfo.Name,
                WorldId = playerInfo.WorldId,
            });
        }

        config.Players = new Dictionary<Guid, dynamic>();
    }
}