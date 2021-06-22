using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Newtonsoft.Json;
// ReSharper disable InconsistentNaming

namespace TextToTalk
{
    public class PluginConfiguration : IPluginConfiguration
    {
        private const string DefaultEnabledChatTypesPreset = "Default";

        public int Version { get; set; }

        public bool Enabled { get; set; }

        public bool UseKeybind { get; set; }
        public VirtualKey.Enum ModifierKey { get; set; }
        public VirtualKey.Enum MajorKey { get; set; }

        [Obsolete("Use EnabledChatTypesPresets.")]
        public bool EnableAllChatTypes { get; set; }

        [Obsolete("Use EnabledChatTypesPresets.")]
        public IList<int> EnabledChatTypes { get; set; }

        public bool MigratedTo1_5 { get; set; }

        public IList<Trigger> Bad { get; set; }
        public IList<Trigger> Good { get; set; }

        public string CurrentEnabledChatTypesPreset { get; set; }
        public IDictionary<string, EnabledChatTypesPreset> EnabledChatTypesPresets { get; set; }

        public int Rate { get; set; }
        public int Volume { get; set; }

        public string VoiceName { get; set; }

        public bool UseWebsocket { get; set; }

        public bool NameNpcWithSay { get; set; } = true;

        public bool DisallowMultipleSay { get; set; }

        public bool ReadFromQuestTalkAddon { get; set; } = true;

        /// <summary>
        /// <c>true</c> if it is not the first time, <c>false</c> if the first time handler has run before. This was named horribly.
        /// </summary>
        private bool FirstTime { get; set; }

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public PluginConfiguration()
        {
            Enabled = true;

            Bad = new List<Trigger>();
            Good = new List<Trigger>();

            using var ss = new SpeechSynthesizer();
            Rate = ss.Rate;
            Volume = ss.Volume;

            ModifierKey = VirtualKey.Enum.VkControl;
            MajorKey = VirtualKey.Enum.VkN;

            CurrentEnabledChatTypesPreset = DefaultEnabledChatTypesPreset;
            EnabledChatTypesPresets = new Dictionary<string, EnabledChatTypesPreset>();

            VoiceName = ss.GetInstalledVoices().First().VoiceInfo.Name;
        }

        public void Initialize(DalamudPluginInterface pi)
        {
            this.pluginInterface = pi;

            if (!FirstTime)
            {
                EnabledChatTypesPresets[DefaultEnabledChatTypesPreset] = new EnabledChatTypesPreset
                {
                    EnabledChatTypes = new List<int>
                    {
                        (int) XivChatType.Say,
                        (int) XivChatType.Shout,
                        (int) XivChatType.Party,
                        (int) AdditionalChatTypes.Enum.BeneficialEffectOnYou,
                        (int) AdditionalChatTypes.Enum.BeneficialEffectOnYouEnded,
                        (int) AdditionalChatTypes.Enum.BeneficialEffectOnOtherPlayer,
                    },
                };

                FirstTime = true;
                MigratedTo1_5 = true;
            }

            if (FirstTime && !MigratedTo1_5)
            {
                EnabledChatTypesPresets[DefaultEnabledChatTypesPreset] = new EnabledChatTypesPreset
                {
#pragma warning disable CS1062 // The best overloaded Add method for the collection initializer element is obsolete
#pragma warning disable 618
                    EnableAllChatTypes = EnableAllChatTypes,
                    EnabledChatTypes = EnabledChatTypes,
#pragma warning restore 618
#pragma warning restore CS1062 // The best overloaded Add method for the collection initializer element is obsolete
                };

                MigratedTo1_5 = true;
            }

            this.pluginInterface.SavePluginConfig(this);
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }

        public EnabledChatTypesPreset GetCurrentEnabledChatTypesPreset()
        {
            return EnabledChatTypesPresets[CurrentEnabledChatTypesPreset];
        }

        public void SetCurrentEnabledChatTypesPreset(string presetName)
        {
            CurrentEnabledChatTypesPreset = presetName;
        }
    }
}
