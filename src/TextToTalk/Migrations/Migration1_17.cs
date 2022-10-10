using TextToTalk.Backends;
using TextToTalk.Backends.System;

namespace TextToTalk.Migrations;

public class Migration1_17 : IConfigurationMigration
{
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return !config.MigratedTo1_17;
    }

    public void Migrate(PluginConfiguration config)
    {
        for (var i = 0; i < config.VoicePresets.Count; i++)
        {
            var preset = config.VoicePresets[i];
            
            // Check if this is an instance of VoicePreset directly (or a superclass),
            // rather than one of its inheritors.
            if (preset.GetType().IsAssignableFrom(typeof(VoicePreset)))
            {
                config.VoicePresets[i] = new SystemVoicePreset
                {
                    Id = preset.Id,
                    Name = preset.Name,
#pragma warning disable 618
                    Rate = preset.ObsoleteRate,
                    VoiceName = preset.ObsoleteVoiceName,
                    Volume = preset.ObsoleteVolume,
#pragma warning restore 618
                    EnabledBackend = TTSBackend.System,
                };
            }
        }
        
        config.MigratedTo1_17 = true;
    }
}