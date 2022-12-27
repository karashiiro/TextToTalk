using System.Collections.Generic;
using TextToTalk.Backends;
#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_18_2 : IConfigurationMigration
{
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return !config.MigratedTo1_18_2;
    }

    public void Migrate(PluginConfiguration config)
    {
        // This can be null if the configuration file was created on v1.18.1
        if (config.VoicePresetConfig.CurrentVoicePresets != null)
        {
            foreach (var (backend, id) in config.VoicePresetConfig.CurrentVoicePresets)
            {
                config.VoicePresetConfig.CurrentVoicePreset[backend] = id;
            }
        }

        config.MigratedTo1_18_2 = true;
    }
}