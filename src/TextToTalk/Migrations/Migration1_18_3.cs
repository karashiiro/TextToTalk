using System.Collections.Generic;
using TextToTalk.Backends;
#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_18_3 : IConfigurationMigration
{
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return !config.MigratedTo1_18_3;
    }

    public void Migrate(PluginConfiguration config)
    {
        if (config.VoicePresetConfig.UngenderedVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in config.VoicePresetConfig.UngenderedVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.VoicePresetConfig.GetUngenderedPresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.VoicePresetConfig.UngenderedVoicePresets[backend] == null)
                    {
                        config.VoicePresetConfig.UngenderedVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.VoicePresetConfig.GetUngenderedPresets(backend).Add(idv);
                        }
                    }
                }
            }
        }
        
        if (config.VoicePresetConfig.MaleVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in config.VoicePresetConfig.MaleVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.VoicePresetConfig.GetMalePresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.VoicePresetConfig.MaleVoicePresets[backend] == null)
                    {
                        config.VoicePresetConfig.MaleVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.VoicePresetConfig.GetMalePresets(backend).Add(idv);
                        }
                    }
                }
            }
        }
        
        if (config.VoicePresetConfig.FemaleVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in config.VoicePresetConfig.FemaleVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.VoicePresetConfig.GetFemalePresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.VoicePresetConfig.FemaleVoicePresets[backend] == null)
                    {
                        config.VoicePresetConfig.FemaleVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.VoicePresetConfig.GetFemalePresets(backend).Add(idv);
                        }
                    }
                }
            }
        }

        config.MigratedTo1_18_3 = true;
    }
}