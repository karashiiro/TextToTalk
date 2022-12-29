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
        if (config.GetVoiceConfig().CurrentVoicePresets != null)
        {
            foreach (var (backend, id) in config.GetVoiceConfig().CurrentVoicePresets)
            {
                config.GetVoiceConfig().CurrentVoicePreset[backend] = id;
            }
        }

        if (config.GetVoiceConfig().UngenderedVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in config.GetVoiceConfig().UngenderedVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.GetVoiceConfig().GetUngenderedPresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.GetVoiceConfig().UngenderedVoicePresets[backend] == null)
                    {
                        config.GetVoiceConfig().UngenderedVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.GetVoiceConfig().GetUngenderedPresets(backend).Add(idv);
                        }
                    }
                }
            }
        }
        
        if (config.GetVoiceConfig().MaleVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in config.GetVoiceConfig().MaleVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.GetVoiceConfig().GetMalePresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.GetVoiceConfig().MaleVoicePresets[backend] == null)
                    {
                        config.GetVoiceConfig().MaleVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.GetVoiceConfig().GetMalePresets(backend).Add(idv);
                        }
                    }
                }
            }
        }
        
        if (config.GetVoiceConfig().FemaleVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in config.GetVoiceConfig().FemaleVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.GetVoiceConfig().GetFemalePresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.GetVoiceConfig().FemaleVoicePresets[backend] == null)
                    {
                        config.GetVoiceConfig().FemaleVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.GetVoiceConfig().GetFemalePresets(backend).Add(idv);
                        }
                    }
                }
            }
        }

        config.MigratedTo1_18_2 = true;
    }
}