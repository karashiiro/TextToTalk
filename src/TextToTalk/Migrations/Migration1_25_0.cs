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
        foreach (var (_, playerInfo) in config.Players)
        {
            this.playerCollection.StorePlayer(new Player
            {
                Id = playerInfo.LocalId,
                Name = playerInfo.Name,
                WorldId = playerInfo.WorldId,
            });
        }
        
        foreach (var (playerId, voiceId) in config.PlayerVoicePresets)
        {
            this.playerCollection.StorePlayerVoice(new PlayerVoice
            {
                PlayerId = playerId,
                VoicePresetId = voiceId,
            });
        }
        
        foreach (var (_, npcInfo) in config.Npcs)
        {
            this.npcCollection.StoreNpc(new Npc
            {
                Id = npcInfo.LocalId,
                Name = npcInfo.Name,
            });
        }
        
        foreach (var (npcId, voiceId) in config.NpcVoicePresets)
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