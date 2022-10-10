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
                    config.VoicePresets[i] = new SystemVoicePreset
                    {
                        Id = preset.Id,
                        Name = preset.Name,
                        Rate = preset.ObsoleteRate,
                        VoiceName = preset.ObsoleteVoiceName,
                        Volume = preset.ObsoleteVolume,
                        EnabledBackend = TTSBackend.System,
                    };
                }
            }

            config.CurrentVoicePreset[TTSBackend.System] = config.CurrentVoicePresetId;
            config.UngenderedVoicePreset[TTSBackend.System] = config.UngenderedVoicePresetId;
            config.MaleVoicePreset[TTSBackend.System] = config.MaleVoicePresetId;
            config.FemaleVoicePreset[TTSBackend.System] = config.FemaleVoicePresetId;
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
                config.CurrentVoicePreset[TTSBackend.AmazonPolly] = defaultPreset.Id;
                config.UngenderedVoicePreset[TTSBackend.AmazonPolly] = defaultPreset.Id;
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<PollyVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                malePreset.Volume = config.PollyVolume;
                malePreset.VoiceEngine = config.PollyEngine;
                malePreset.VoiceName = config.PollyVoiceMale ?? VoiceId.Matthew;
                malePreset.PlaybackRate = config.PollyPlaybackRate;
                malePreset.SampleRate = config.PollySampleRate;
                config.MaleVoicePreset[TTSBackend.AmazonPolly] = malePreset.Id;
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<PollyVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                femalePreset.Volume = config.PollyVolume;
                femalePreset.VoiceEngine = config.PollyEngine;
                femalePreset.VoiceName = config.PollyVoiceFemale ?? VoiceId.Matthew;
                femalePreset.PlaybackRate = config.PollyPlaybackRate;
                femalePreset.SampleRate = config.PollySampleRate;
                config.FemaleVoicePreset[TTSBackend.AmazonPolly] = femalePreset.Id;
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
                config.CurrentVoicePreset[TTSBackend.Uberduck] = defaultPreset.Id;
                config.UngenderedVoicePreset[TTSBackend.Uberduck] = defaultPreset.Id;
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<UberduckVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                malePreset.Volume = config.UberduckVolume;
                malePreset.PlaybackRate = config.UberduckPlaybackRate;
                malePreset.VoiceName = config.UberduckVoiceMale ?? "zwf";
                config.MaleVoicePreset[TTSBackend.Uberduck] = malePreset.Id;
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<UberduckVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                femalePreset.Volume = config.UberduckVolume;
                femalePreset.PlaybackRate = config.UberduckPlaybackRate;
                femalePreset.VoiceName = config.UberduckVoiceFemale ?? "zwf";
                config.FemaleVoicePreset[TTSBackend.Uberduck] = femalePreset.Id;
            }
        }
        
        // Add placeholder voice presets for WebSocket clients
        {
            if (config.TryCreateVoicePreset<WebsocketVoicePreset>(out var defaultPreset))
            {
                defaultPreset.Name = "Default";
                config.CurrentVoicePreset[TTSBackend.Websocket] = defaultPreset.Id;
                config.UngenderedVoicePreset[TTSBackend.Websocket] = defaultPreset.Id;
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<WebsocketVoicePreset>(out var malePreset))
            {
                malePreset.Name = "Male";
                config.MaleVoicePreset[TTSBackend.Websocket] = malePreset.Id;
            }
            
            if (config.UseGenderedVoicePresets && config.TryCreateVoicePreset<WebsocketVoicePreset>(out var femalePreset))
            {
                femalePreset.Name = "Female";
                config.FemaleVoicePreset[TTSBackend.Websocket] = femalePreset.Id;
            }
        }
        
        config.MigratedTo1_17 = true;
    }
}