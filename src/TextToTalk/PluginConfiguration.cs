﻿using Amazon.Polly;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using TextToTalk.Backends;
using TextToTalk.Backends.System;
using TextToTalk.GameEnums;
using TextToTalk.Migrations;

// ReSharper disable InconsistentNaming

namespace TextToTalk
{
    public enum FirstOrLastName
    {
        First,
        Last,
    }

    public class PluginConfiguration : IPluginConfiguration
    {
        private const string DefaultPreset = "Default";

        #region Obsolete Members

        [Obsolete("Use EnabledChatTypesPresets.")]
        public bool EnableAllChatTypes { get; set; }

        [Obsolete("Use EnabledChatTypesPresets.")]
        // ReSharper disable once CollectionNeverUpdated.Global
        public IList<int> EnabledChatTypes { get; set; }
        
        [Obsolete]
        public IList<VoicePreset> VoicePresets { get; set; }

        [Obsolete("Use VoicePresets.")] public int Rate { get; set; }

        [Obsolete("Use VoicePresets.")] public int Volume { get; set; }

        [Obsolete("Use VoicePresets.")] public string VoiceName { get; set; }

        [Obsolete("Use Backend.")] public bool UseWebsocket { get; set; }

        [Obsolete("Use PollyLexiconFiles.")] public List<string> PollyLexicons { get; set; }

        /// <summary>
        /// <c>true</c> if it is not the first time, <c>false</c> if the first time handler has not run before. This was named horribly.
        /// </summary>
        [Obsolete("Use InitializedEver.")]
        public bool FirstTime { get; set; }

        [Obsolete] public string PollyVoice { get; set; } = VoiceId.Matthew;

        [Obsolete] public string PollyVoiceUngendered { get; set; } = VoiceId.Matthew;

        [Obsolete] public string PollyVoiceMale { get; set; } = VoiceId.Matthew;

        [Obsolete] public string PollyVoiceFemale { get; set; } = VoiceId.Matthew;

        [Obsolete] public string PollyEngine { get; set; } = Engine.Neural;

        [Obsolete] public int PollySampleRate { get; set; } = 22050;

        [Obsolete] public float PollyVolume { get; set; } = 1.0f;

        [Obsolete] public int PollyPlaybackRate { get; set; } = 100;

        [Obsolete] public string UberduckVoice { get; set; } = "zwf";

        [Obsolete] public string UberduckVoiceUngendered { get; set; } = "zwf";

        [Obsolete] public string UberduckVoiceMale { get; set; } = "zwf";

        [Obsolete] public string UberduckVoiceFemale { get; set; } = "zwf";

        [Obsolete] public float UberduckVolume { get; set; } = 1.0f;

        [Obsolete] public int UberduckPlaybackRate { get; set; } = 100;

        [Obsolete] public int CurrentVoicePresetId { get; set; }
        [Obsolete] public int UngenderedVoicePresetId { get; set; }
        [Obsolete] public int MaleVoicePresetId { get; set; }
        [Obsolete] public int FemaleVoicePresetId { get; set; }

        #endregion

        public int Version { get; set; }

        public bool Enabled { get; set; }

        public bool UseKeybind { get; set; }
        public VirtualKey.Enum ModifierKey { get; set; }
        public VirtualKey.Enum MajorKey { get; set; }

        public bool MigratedTo1_5 { get; set; }
        public bool MigratedTo1_6 { get; set; }
        public bool MigratedTo1_17 { get; set; }

        public IList<Trigger> Bad { get; set; }
        public IList<Trigger> Good { get; set; }

        public int CurrentPresetId { get; set; }
        public IList<EnabledChatTypesPreset> EnabledChatTypesPresets { get; set; }

        public int WebsocketPort { get; set; }

        public bool NameNpcWithSay { get; set; } = true;
        public bool EnableNameWithSay { get; set; } = true;
        public bool DisallowMultipleSay { get; set; }
        public bool SayPlayerWorldName { get; set; } = true;
        public bool SayPartialName { get; set; } = false;
        public FirstOrLastName OnlySayFirstOrLastName { get; set; } = FirstOrLastName.First;

        public bool ReadFromQuestTalkAddon { get; set; } = true;
        public bool CancelSpeechOnTextAdvance { get; set; }

        public bool UseGenderedVoicePresets { get; set; }

        public TTSBackend Backend { get; set; }
        [JsonIgnore] public VoicePresetConfiguration VoicePresetConfig { get; set; }

        public IList<string> Lexicons { get; set; }

        public string PollyRegion { get; set; }
        public IList<string> PollyLexiconFiles { get; set; }

        public bool RemoveStutterEnabled { get; set; } = true;

        public IDictionary<string, IDictionary<TTSBackend, bool>> RemoteLexiconEnabledBackends { get; set; }

        public bool UsePlayerRateLimiter { get; set; }
        public float MessagesPerSecond { get; set; } = 5;

        public IDictionary<Guid, PlayerInfo> Players { get; set; }
        public IDictionary<Guid, int> PlayerVoicePresets { get; set; }

        [JsonIgnore]
        public bool InitializedEver
        {
#pragma warning disable 618
            get => FirstTime;
            set => FirstTime = value;
#pragma warning restore 618
        }

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        [JsonIgnore] private object cfgLock;

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
            this.cfgLock = true;

            VoicePresetConfig = VoicePresetConfiguration.LoadFromFile(GetVoicePresetsConfigPath());

            EnabledChatTypesPresets ??= new List<EnabledChatTypesPreset>();

            PollyLexiconFiles ??= new List<string>();
            Lexicons ??= new List<string>();

            RemoteLexiconEnabledBackends ??= new Dictionary<string, IDictionary<TTSBackend, bool>>();

            Players ??= new Dictionary<Guid, PlayerInfo>();
            PlayerVoicePresets ??= new Dictionary<Guid, int>();

            if (!InitializedEver)
            {
                EnabledChatTypesPresets.Add(new EnabledChatTypesPreset
                {
                    Id = 0,
                    EnabledChatTypes = new List<int>
                    {
                        (int)XivChatType.Say,
                        (int)XivChatType.Shout,
                        (int)XivChatType.Party,
                        (int)AdditionalChatType.BeneficialEffectOnYou,
                        (int)AdditionalChatType.BeneficialEffectOnYouEnded,
                        (int)AdditionalChatType.BeneficialEffectOnOtherPlayer,
                    },
                    Name = DefaultPreset,
                    UseKeybind = false,
                    ModifierKey = VirtualKey.Enum.VkShift,
                    MajorKey = VirtualKey.Enum.Vk0,
                });

                try
                {
                    var defaultPreset = new SystemVoicePreset
                    {
                        Id = 0,
                        Name = DefaultPreset,
                        EnabledBackend = TTSBackend.System,
                    };

                    if (defaultPreset.TrySetDefaultValues())
                    {
                        VoicePresetConfig.VoicePresets.Add(defaultPreset);
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to set values for default preset.");
                    }
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to create default voice preset.");
                }

                InitializedEver = true;
                MigratedTo1_5 = true;
                MigratedTo1_6 = true;
                MigratedTo1_17 = true;
            }

            if (InitializedEver)
            {
                var migrations = new IConfigurationMigration[]
                    { new Migration1_5(), new Migration1_6(), new Migration1_17() };
                foreach (var migration in migrations)
                {
                    if (migration.ShouldMigrate(this))
                    {
                        migration.Migrate(this);
                    }
                }
            }

            Save();
        }

        public void Save()
        {
            lock (this.cfgLock)
            {
                this.pluginInterface.SavePluginConfig(this);
                VoicePresetConfig.SaveToFile(GetVoicePresetsConfigPath());
            }
        }
        
        private string GetVoicePresetsConfigPath()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), "VoicePresets.json");
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

        public IEnumerable<VoicePreset> GetVoicePresetsForBackend(TTSBackend backend)
        {
            return VoicePresetConfig.VoicePresets.Where(p => p.EnabledBackend == backend);
        }

        public void SetCurrentEnabledChatTypesPreset(int presetId)
        {
            CurrentPresetId = presetId;
        }
       
        public TPreset GetCurrentVoicePreset<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresetConfig.VoicePresets.FirstOrDefault(p =>
                p.Id == VoicePresetConfig.CurrentVoicePreset[Backend] && p.EnabledBackend == Backend) as TPreset;
        }

        public TPreset[] GetCurrentUngenderedVoicePresets<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresetConfig.VoicePresets.Where(p =>
                    VoicePresetConfig.UngenderedVoicePresets[Backend].Contains(p.Id) && p.EnabledBackend == Backend)
                .Cast<TPreset>().ToArray();
        }

        public TPreset[] GetCurrentMaleVoicePresets<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresetConfig.VoicePresets.Where(p =>
                    VoicePresetConfig.MaleVoicePresets[Backend].Contains(p.Id) && p.EnabledBackend == Backend)
                .Cast<TPreset>().ToArray();
        }

        public TPreset[] GetCurrentFemaleVoicePresets<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresetConfig.VoicePresets.Where(p =>
                    VoicePresetConfig.FemaleVoicePresets[Backend].Contains(p.Id) && p.EnabledBackend == Backend)
                .Cast<TPreset>().ToArray();
        }

        public int GetHighestVoicePresetId()
        {
            return VoicePresetConfig.VoicePresets.Select(p => p.Id).Max();
        }

        public bool TryCreateVoicePreset<TPreset>(out TPreset preset) where TPreset : VoicePreset, new()
        {
            var highestId = GetHighestVoicePresetId();
            preset = new TPreset
            {
                Id = highestId + 1,
                Name = "New preset",
            };

            if (preset.TrySetDefaultValues())
            {
                VoicePresetConfig.VoicePresets.Add(preset);
                return true;
            }

            return false;
        }

        public void SetCurrentVoicePreset(int presetId)
        {
            VoicePresetConfig.CurrentVoicePreset[Backend] = presetId;
        }
    }
}