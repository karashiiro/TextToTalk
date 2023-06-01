using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Service;
#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_25_0 : IConfigurationMigration
{
    private readonly PlayerCollection playerCollection;
    private readonly NpcCollection npcCollection;

    public Migration1_25_0(PlayerCollection playerCollection, NpcCollection npcCollection)
    {
        this.playerCollection = playerCollection;
        this.npcCollection = npcCollection;
    }
    
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return !config.MigratedTo1_25_0;
    }

    public void Migrate(PluginConfiguration config)
    {
        var players = config.Players ?? new Dictionary<Guid, dynamic>();
        foreach (var (_, playerInfo) in players)
        {
            // Due to the old types being removed, this can degrade to JObject, which
            // needs to be handled accordingly.
            if (playerInfo is JObject jsonPlayerInfo)
            {
                if (!jsonPlayerInfo.TryGetValue("Name", out var name))
                {
                    continue;
                }
            
                this.playerCollection.StorePlayer(new Player
                {
                    Id = jsonPlayerInfo["LocalId"]?.Value<Guid>() ?? Guid.NewGuid(),
                    Name = name.Value<string>() ?? "",
                    WorldId = jsonPlayerInfo["WorldId"]?.Value<uint>() ?? 81,
                });
            }
            else
            {
                this.playerCollection.StorePlayer(new Player
                {
                    Id = playerInfo.LocalId,
                    Name = playerInfo.Name,
                    WorldId = playerInfo.WorldId,
                });
            }
        }
        
        foreach (var (playerId, voiceId) in config.PlayerVoicePresets ?? new Dictionary<Guid, int>())
        {
            this.playerCollection.StorePlayerVoice(new PlayerVoice
            {
                PlayerId = playerId,
                VoicePresetId = voiceId,
            });
        }

        var npcs = config.Npcs ?? new Dictionary<Guid, dynamic>();
        foreach (var (_, npcInfo) in npcs)
        {
            if (npcInfo is JObject jsonNpcInfo)
            {
                if (!jsonNpcInfo.TryGetValue("Name", out var name))
                {
                    continue;
                }
                
                this.npcCollection.StoreNpc(new Npc
                {
                    Id = jsonNpcInfo["LocalId"]?.Value<Guid>() ?? Guid.NewGuid(),
                    Name = name.Value<string>() ?? "",
                });
            }
            else
            {
                this.npcCollection.StoreNpc(new Npc
                {
                    Id = npcInfo.LocalId,
                    Name = npcInfo.Name,
                });
            }
        }
        
        foreach (var (npcId, voiceId) in config.NpcVoicePresets ?? new Dictionary<Guid, int>())
        {
            this.npcCollection.StoreNpcVoice(new NpcVoice
            {
                NpcId = npcId,
                VoicePresetId = voiceId,
            });
        }
        
        config.Players = new Dictionary<Guid, dynamic>();
        config.PlayerVoicePresets = new Dictionary<Guid, int>();
        config.Npcs = new Dictionary<Guid, dynamic>();
        config.NpcVoicePresets = new Dictionary<Guid, int>();

        config.MigratedTo1_25_0 = true;
    }
}