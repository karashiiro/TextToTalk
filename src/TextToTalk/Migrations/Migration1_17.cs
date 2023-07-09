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
            var voicePresets = config.VoicePresets ?? new List<VoicePreset>();
            for (var i = 0; i < voicePresets.Count; i++)
            {
                var preset = voicePresets[i];
            
                // Check if this is an instance of VoicePreset directly, rather
                // than one of its inheritors.
                if (preset.GetType() == typeof(VoicePreset))
                {
                    DetailedLog.Info($"Migrating preset {preset.Name}");
                    config.GetVoiceConfig().VoicePresets.Add(new SystemVoicePreset
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

            config.GetVoiceConfig().CurrentVoicePreset[TTSBackend.System] = config.CurrentVoicePresetId;
            config.GetVoiceConfig().UngenderedVoicePresets[TTSBackend.System] =
                new SortedSet<int> { config.UngenderedVoicePresetId };
            config.GetVoiceConfig().MaleVoicePresets[TTSBackend.System] =
                new SortedSet<int> { config.MaleVoicePresetId };
            config.GetVoiceConfig().FemaleVoicePresets[TTSBackend.System] =
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
                config.GetVoiceConfig().CurrentVoicePreset[TTSBackend.AmazonPolly] = defaultPreset.Id;
                config.GetVoiceConfig().UngenderedVoicePresets[TTSBackend.AmazonPolly] =
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
                config.GetVoiceConfig().MaleVoicePresets[TTSBackend.AmazonPolly] =
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
                config.GetVoiceConfig().FemaleVoicePresets[TTSBackend.AmazonPolly] =
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
                config.GetVoiceConfig().CurrentVoicePreset[TTSBackend.Uberduck] = defaultPreset.Id;
                config.GetVoiceConfig().UngenderedVoicePresets[TTSBackend.Uberduck] =
                    new SortedSet<int> { defaultPreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<UberduckVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                malePreset.Volume = config.UberduckVolume;
                malePreset.PlaybackRate = config.UberduckPlaybackRate;
                malePreset.VoiceName = config.UberduckVoiceMale ?? "zwf";
                config.GetVoiceConfig().MaleVoicePresets[TTSBackend.Uberduck] = new SortedSet<int> { malePreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<UberduckVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                femalePreset.Volume = config.UberduckVolume;
                femalePreset.PlaybackRate = config.UberduckPlaybackRate;
                femalePreset.VoiceName = config.UberduckVoiceFemale ?? "zwf";
                config.GetVoiceConfig().FemaleVoicePresets[TTSBackend.Uberduck] =
                    new SortedSet<int> { femalePreset.Id };
            }
        }
        
        // Add placeholder voice presets for WebSocket clients
        {
            if (config.TryCreateVoicePreset<WebsocketVoicePreset>(out var defaultPreset))
            {
                defaultPreset.Name = "Default";
                config.GetVoiceConfig().CurrentVoicePreset[TTSBackend.Websocket] = defaultPreset.Id;
                config.GetVoiceConfig().UngenderedVoicePresets[TTSBackend.Websocket] =
                    new SortedSet<int> { defaultPreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<WebsocketVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                config.GetVoiceConfig().MaleVoicePresets[TTSBackend.Websocket] = new SortedSet<int> { malePreset.Id };
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<WebsocketVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                config.GetVoiceConfig().FemaleVoicePresets[TTSBackend.Websocket] =
                    new SortedSet<int> { femalePreset.Id };
            }
        }
        
        config.MigratedTo1_17 = true;
    }
    
    private int GetHighestVoicePresetIdObsolete(PluginConfiguration config)
    {
        return config.VoicePresets?.Select(p => p.Id).Max() ?? 0;
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
            config.GetVoiceConfig().VoicePresets.Add(preset);
            return true;
        }

        return false;
    }

}