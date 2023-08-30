using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CSharp.RuntimeBinder;
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
        foreach (var (playerId, playerInfo) in players)
        {
            // Due to the old types being removed, this can degrade to JObject, which
            // needs to be handled accordingly.
            try
            {
                this.playerCollection.StorePlayer(new Player
                {
                    Id = playerId,
                    Name = playerInfo.Name,
                    WorldId = playerInfo.WorldId,
                });
            }
            catch (RuntimeBinderException)
            {
                if (!playerInfo.TryGetValue("Name", out dynamic name))
                {
                    continue;
                }
            
                this.playerCollection.StorePlayer(new Player
                {
                    Id = playerId,
                    Name = name.Value<string>() ?? "",
                    WorldId = playerInfo["WorldId"]?.Value<uint>() ?? 81,
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
        foreach (var (npcId, npcInfo) in npcs)
        {
            try
            {
                this.npcCollection.StoreNpc(new Npc
                {
                    Id = npcId,
                    Name = npcInfo.Name,
                });
            }
            catch (RuntimeBinderException)
            {
                // This degraded to a JObject since the original type was deleted and
                // the new field type is dynamic.
                if (!npcInfo.TryGetValue("Name", out dynamic name))
                {
                    continue;
                }
                
                this.npcCollection.StoreNpc(new Npc
                {
                    Id = npcId,
                    Name = name.Value<string>() ?? "",
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