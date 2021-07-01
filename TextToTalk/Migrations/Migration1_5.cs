using System.Linq;

namespace TextToTalk.Migrations
{
    public class Migration1_5 : IConfigurationMigration
    {
        public bool ShouldMigrate(PluginConfiguration config)
        {
            return !config.MigratedTo1_5;
        }

        public void Migrate(PluginConfiguration config)
        {
            config.EnabledChatTypesPresets.Add(new EnabledChatTypesPreset
            {
                Id = 0,
#pragma warning disable CS1062 // The best overloaded Add method for the collection initializer element is obsolete
#pragma warning disable 618
                EnableAllChatTypes = config.EnableAllChatTypes,
                EnabledChatTypes = config.EnabledChatTypes.ToList(),
#pragma warning restore 618
#pragma warning restore CS1062 // The best overloaded Add method for the collection initializer element is obsolete
                Name = "Default",
                UseKeybind = false,
                ModifierKey = VirtualKey.Enum.VkShift,
                MajorKey = VirtualKey.Enum.Vk0,
            });

            config.VoicePresets.Add(new VoicePreset
            {
                Id = 0,
                Name = "Default",
#pragma warning disable CS1062 // The best overloaded Add method for the collection initializer element is obsolete
#pragma warning disable 618
                Rate = config.Rate,
                Volume = config.Volume,
                VoiceName = config.VoiceName,
#pragma warning restore 618
#pragma warning restore CS1062 // The best overloaded Add method for the collection initializer element is obsolete
            });

            config.MigratedTo1_5 = true;
        }
    }
}