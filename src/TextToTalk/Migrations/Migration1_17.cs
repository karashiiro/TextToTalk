using System.Collections.Generic;
using System.Linq;
using Amazon.Polly;
using Dalamud.Logging;
using TextToTalk.Backends;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;
#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_17 : IConfigurationMigration
{
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return !config.MigratedTo1_17;
    }

    public void Migrate(PluginConfiguration config)
    {
        // Migrate System voice configuration
        {
            for (var i = 0; i < config.VoicePresets.Count; i++)
            {
                var preset = config.VoicePresets[i];
            
                // Check if this is an instance of VoicePreset directly, rather
                // than one of its inheritors.
                if (preset.GetType() == typeof(VoicePreset))
                {
                    PluginLog.Log($"Migrating preset {preset.Name}");
                    config.VoicePresetConfig.VoicePresets.Add(new SystemVoicePreset
                    {
                        Id = preset.Id,
                        Name = preset.Name,
                        Rate = preset.ObsoleteRate,
                        VoiceName = preset.ObsoleteVoiceName,
                        Volume = preset.ObsoleteVolume,
                        EnabledBackend = TTSBackend.System,
                    });
                }
            }

            config.VoicePresetConfig.CurrentVoicePreset[TTSBackend.System] = config.CurrentVoicePresetId;
            config.VoicePresetConfig.UngenderedVoicePresets[TTSBackend.System] =
                new SortedSet<int> { config.UngenderedVoicePresetId };
            config.VoicePresetConfig.MaleVoicePresets[TTSBackend.System] =
                new SortedSet<int> { config.MaleVoicePresetId };
            config.VoicePresetConfig.FemaleVoicePresets[TTSBackend.System] =
                new SortedSet<int> { config.FemaleVoicePresetId };
        }

        // Migrate Polly voice configuration
        {
            if (config.TryCreateVoicePreset<PollyVoicePreset>(out var defaultPreset))
            {
                defaultPreset.Name = config.PollyVoiceUngendered is not null ? "Ungendered" : "Default";
                defaultPreset.Volume = config.PollyVolume;
                defaultPreset.VoiceEngine = config.PollyEngine;
                defaultPreset.VoiceName = config.PollyVoiceUngendered ?? config.PollyVoice ?? VoiceId.Matthew;
                defaultPreset.PlaybackRate = config.PollyPlaybackRate;
                defaultPreset.SampleRate = config.PollySampleRate;
                config.VoicePresetConfig.CurrentVoicePreset[TTSBackend.AmazonPolly] = defaultPreset.Id;
                config.VoicePresetConfig.UngenderedVoicePresets[TTSBackend.AmazonPolly] =
                    new SortedSet<int> { defaultPreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<PollyVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                malePreset.Volume = config.PollyVolume;
                malePreset.VoiceEngine = config.PollyEngine;
                malePreset.VoiceName = config.PollyVoiceMale ?? VoiceId.Matthew;
                malePreset.PlaybackRate = config.PollyPlaybackRate;
                malePreset.SampleRate = config.PollySampleRate;
                config.VoicePresetConfig.MaleVoicePresets[TTSBackend.AmazonPolly] =
                    new SortedSet<int> { malePreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<PollyVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                femalePreset.Volume = config.PollyVolume;
                femalePreset.VoiceEngine = config.PollyEngine;
                femalePreset.VoiceName = config.PollyVoiceFemale ?? VoiceId.Matthew;
                femalePreset.PlaybackRate = config.PollyPlaybackRate;
                femalePreset.SampleRate = config.PollySampleRate;
                config.VoicePresetConfig.FemaleVoicePresets[TTSBackend.AmazonPolly] =
                    new SortedSet<int> { femalePreset.Id };
            }
        }
        
        // Migrate Uberduck voice configuration
        {
            if (config.TryCreateVoicePreset<UberduckVoicePreset>(out var defaultPreset))
            {
                defaultPreset.Name = config.UberduckVoiceUngendered is not null ? "Ungendered" : "Default";
                defaultPreset.Volume = config.UberduckVolume;
                defaultPreset.PlaybackRate = config.UberduckPlaybackRate;
                defaultPreset.VoiceName = config.UberduckVoiceUngendered ?? config.UberduckVoice ?? "zwf";
                config.VoicePresetConfig.CurrentVoicePreset[TTSBackend.Uberduck] = defaultPreset.Id;
                config.VoicePresetConfig.UngenderedVoicePresets[TTSBackend.Uberduck] =
                    new SortedSet<int> { defaultPreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<UberduckVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                malePreset.Volume = config.UberduckVolume;
                malePreset.PlaybackRate = config.UberduckPlaybackRate;
                malePreset.VoiceName = config.UberduckVoiceMale ?? "zwf";
                config.VoicePresetConfig.MaleVoicePresets[TTSBackend.Uberduck] = new SortedSet<int> { malePreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<UberduckVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                femalePreset.Volume = config.UberduckVolume;
                femalePreset.PlaybackRate = config.UberduckPlaybackRate;
                femalePreset.VoiceName = config.UberduckVoiceFemale ?? "zwf";
                config.VoicePresetConfig.FemaleVoicePresets[TTSBackend.Uberduck] =
                    new SortedSet<int> { femalePreset.Id };
            }
        }
        
        // Add placeholder voice presets for WebSocket clients
        {
            if (config.TryCreateVoicePreset<WebsocketVoicePreset>(out var defaultPreset))
            {
                defaultPreset.Name = "Default";
                config.VoicePresetConfig.CurrentVoicePreset[TTSBackend.Websocket] = defaultPreset.Id;
                config.VoicePresetConfig.UngenderedVoicePresets[TTSBackend.Websocket] =
                    new SortedSet<int> { defaultPreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<WebsocketVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                config.VoicePresetConfig.MaleVoicePresets[TTSBackend.Websocket] = new SortedSet<int> { malePreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<WebsocketVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                config.VoicePresetConfig.FemaleVoicePresets[TTSBackend.Websocket] =
                    new SortedSet<int> { femalePreset.Id };
            }
        }
        
        config.MigratedTo1_17 = true;
    }
    
    private int GetHighestVoicePresetIdObsolete(PluginConfiguration config)
    {
        return config.VoicePresets.Select(p => p.Id).Max();
    }
    
    private bool TryCreateVoicePresetObsolete<TPreset>(PluginConfiguration config, out TPreset preset) where TPreset : VoicePreset, new()
    {
        var highestId = GetHighestVoicePresetIdObsolete(config);
        preset = new TPreset
        {
            Id = highestId + 1,
            Name = "New preset",
        };

        if (preset.TrySetDefaultValues())
        {
            config.VoicePresetConfig.VoicePresets.Add(preset);
            return true;
        }

        return false;
    }

}