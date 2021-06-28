using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using TextToTalk.Migrations;

// ReSharper disable InconsistentNaming

namespace TextToTalk
{
    public class PluginConfiguration : IPluginConfiguration
    {
        private const string DefaultPreset = "Default";

        public int Version { get; set; }

        public bool Enabled { get; set; }

        public bool UseKeybind { get; set; }
        public VirtualKey.Enum ModifierKey { get; set; }
        public VirtualKey.Enum MajorKey { get; set; }

        [Obsolete("Use EnabledChatTypesPresets.")]
        public bool EnableAllChatTypes { get; set; }

        [Obsolete("Use EnabledChatTypesPresets.")]
        // ReSharper disable once CollectionNeverUpdated.Global
        public IList<int> EnabledChatTypes { get; set; }

        public bool MigratedTo1_5 { get; set; }

        public IList<Trigger> Bad { get; set; }
        public IList<Trigger> Good { get; set; }

        public int CurrentPresetId { get; set; }
        public IList<EnabledChatTypesPreset> EnabledChatTypesPresets { get; set; }

        [Obsolete("Use VoicePresets.")]
        public int Rate { get; set; }
        
        [Obsolete("Use VoicePresets.")]
        public int Volume { get; set; }
        
        [Obsolete("Use VoicePresets.")]
        public string VoiceName { get; set; }

        public int CurrentVoicePresetId { get; set; }
        public IList<VoicePreset> VoicePresets { get; set; }

        public bool UseWebsocket { get; set; }

        public bool NameNpcWithSay { get; set; } = true;

        public bool EnableNameWithSay { get; set; } = true;

        public bool DisallowMultipleSay { get; set; }

        public bool ReadFromQuestTalkAddon { get; set; } = true;

        /// <summary>
        /// <c>true</c> if it is not the first time, <c>false</c> if the first time handler has run before. This was named horribly.
        /// </summary>
        public bool FirstTime { get; set; }

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public PluginConfiguration()
        {
            Enabled = true;

            Bad = new List<Trigger>();
            Good = new List<Trigger>();

            ModifierKey = VirtualKey.Enum.VkControl;
            MajorKey = VirtualKey.Enum.VkN;
        }

        public void Initialize(DalamudPluginInterface pi)
        {
            this.pluginInterface = pi;

            EnabledChatTypesPresets ??= new List<EnabledChatTypesPreset>();
            VoicePresets ??= new List<VoicePreset>();

            if (!FirstTime)
            {
                EnabledChatTypesPresets.Add(new EnabledChatTypesPreset
                {
                    Id = 0,
                    EnabledChatTypes = new List<int>
                    {
                        (int) XivChatType.Say,
                        (int) XivChatType.Shout,
                        (int) XivChatType.Party,
                        (int) AdditionalChatTypes.Enum.BeneficialEffectOnYou,
                        (int) AdditionalChatTypes.Enum.BeneficialEffectOnYouEnded,
                        (int) AdditionalChatTypes.Enum.BeneficialEffectOnOtherPlayer,
                    },
                    Name = DefaultPreset,
                    UseKeybind = false,
                    ModifierKey = VirtualKey.Enum.VkShift,
                    MajorKey = VirtualKey.Enum.Vk0,
                });

                using var ss = new SpeechSynthesizer();
                VoicePresets.Add(new VoicePreset
                {
                    Id = 0,
                    Rate = ss.Rate,
                    Volume = ss.Volume,
                    VoiceName = ss.GetInstalledVoices().First().VoiceInfo.Name,
                    Name = DefaultPreset,
                });

                FirstTime = true;
                MigratedTo1_5 = true;
            }

            if (FirstTime && !MigratedTo1_5)
            {
                new Migration1_5().Migrate(this);
                MigratedTo1_5 = true;
            }

            Save();
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }

        public EnabledChatTypesPreset GetCurrentEnabledChatTypesPreset()
        {
            return EnabledChatTypesPresets.First(p => p.Id == CurrentPresetId);
        }

        public EnabledChatTypesPreset NewChatTypesPreset()
        {
            var highestId = EnabledChatTypesPresets.Select(p => p.Id).Max();
            var preset = new EnabledChatTypesPreset
            {
                Id = highestId + 1,
                EnabledChatTypes = new List<int>(),
                Name = "New preset",
                UseKeybind = false,
                ModifierKey = VirtualKey.Enum.VkShift,
                MajorKey = VirtualKey.Enum.Vk0,
            };

            EnabledChatTypesPresets.Add(preset);

            return preset;
        }

        public void SetCurrentEnabledChatTypesPreset(int presetId)
        {
            CurrentPresetId = presetId;
        }

        public VoicePreset GetCurrentVoicePreset()
        {
            return VoicePresets.First(p => p.Id == CurrentVoicePresetId);
        }

        public VoicePreset NewVoicePreset()
        {
            using var ss = new SpeechSynthesizer();

            var highestId = VoicePresets.Select(p => p.Id).Max();
            var preset = new VoicePreset
            {
                Id = highestId + 1,
                Rate = ss.Rate,
                Volume = ss.Volume,
                VoiceName = ss.GetInstalledVoices().First().VoiceInfo.Name,
                Name = "New preset",
            };

            VoicePresets.Add(preset);

            return preset;
        }

        public void SetCurrentVoicePreset(int presetId)
        {
            CurrentVoicePresetId = presetId;
        }
    }
}
