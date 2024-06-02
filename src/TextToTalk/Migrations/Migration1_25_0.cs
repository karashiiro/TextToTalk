using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json.Linq;
using TextToTalk.Data.Model;
using TextToTalk.Data.Services;

#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_25_0 : IConfigurationMigration
{
    public string Name => "v1.25.0";
    
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
        var players = config.Players ?? new Dictionary<Guid, PlayerInfo>();
        foreach (var (playerId, playerInfo) in players)
        {
            this.playerCollection.StorePlayer(new Player
            {
                Id = playerId,
                Name = playerInfo.Name,
                WorldId = playerInfo.WorldId,
            });
        }
        
        foreach (var (playerId, voiceId) in config.PlayerVoicePresets ?? new Dictionary<Guid, int>())
        {
            this.playerCollection.StorePlayerVoice(new PlayerVoice
            {
                PlayerId = playerId,
                VoicePresetId = voiceId,
            });
        }

        var npcs = config.Npcs ?? new Dictionary<Guid, NpcInfo>();
        foreach (var (npcId, npcInfo) in npcs)
        {
            this.npcCollection.StoreNpc(new Npc
            {
                Id = npcId,
                Name = npcInfo.Name,
            });
        }
        
        foreach (var (npcId, voiceId) in config.NpcVoicePresets ?? new Dictionary<Guid, int>())
        {
            this.npcCollection.StoreNpcVoice(new NpcVoice
            {
                NpcId = npcId,
                VoicePresetId = voiceId,
            });
        }
        
        config.Players = new Dictionary<Guid, PlayerInfo>();
        config.PlayerVoicePresets = new Dictionary<Guid, int>();
        config.Npcs = new Dictionary<Guid, NpcInfo>();
        config.NpcVoicePresets = new Dictionary<Guid, int>();

        config.MigratedTo1_25_0 = true;
    }
}