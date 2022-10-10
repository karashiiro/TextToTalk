using Amazon.Polly;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        Last
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
        public int CurrentVoicePresetId { get; set; }
        public IList<VoicePreset> VoicePresets { get; set; }

        public TTSBackend Backend { get; set; }

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
        public int UngenderedVoicePresetId { get; set; }
        public int MaleVoicePresetId { get; set; }
        public int FemaleVoicePresetId { get; set; }

        public IList<string> Lexicons { get; set; }

        public string PollyVoice { get; set; } = VoiceId.Matthew;
        public string PollyVoiceUngendered { get; set; } = VoiceId.Matthew;
        public string PollyVoiceMale { get; set; } = VoiceId.Matthew;
        public string PollyVoiceFemale { get; set; } = VoiceId.Matthew;
        public string PollyEngine { get; set; } = Engine.Neural;
        public string PollyRegion { get; set; }
        public int PollySampleRate { get; set; } = 22050;
        public float PollyVolume { get; set; } = 1.0f;
        public int PollyPlaybackRate { get; set; } = 100;
        public IList<string> PollyLexiconFiles { get; set; }

        public string UberduckVoice { get; set; } = "zwf";
        public string UberduckVoiceUngendered { get; set; } = "zwf";
        public string UberduckVoiceMale { get; set; } = "zwf";
        public string UberduckVoiceFemale { get; set; } = "zwf";
        public float UberduckVolume { get; set; } = 1.0f;
        public int UberduckPlaybackRate { get; set; } = 100;

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

            EnabledChatTypesPresets ??= new List<EnabledChatTypesPreset>();
            VoicePresets ??= new List<VoicePreset>();

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
                        VoicePresets.Add(defaultPreset);
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
            }
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

        public IList<VoicePreset> GetVoicePresetsForBackend(TTSBackend backend)
        {
            return VoicePresets.Where(p => p.EnabledBackend == backend).ToList();
        }

        public void SetCurrentEnabledChatTypesPreset(int presetId)
        {
            CurrentPresetId = presetId;
        }

        public TPreset GetCurrentVoicePreset<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresets.FirstOrDefault(p => p.Id == CurrentVoicePresetId && p.EnabledBackend == Backend) as TPreset;
        }

        public TPreset GetCurrentUngenderedVoicePreset<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresets.FirstOrDefault(p => p.Id == UngenderedVoicePresetId && p.EnabledBackend == Backend) as TPreset;
        }

        public TPreset GetCurrentMaleVoicePreset<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresets.FirstOrDefault(p => p.Id == MaleVoicePresetId && p.EnabledBackend == Backend) as TPreset;
        }

        public TPreset GetCurrentFemaleVoicePreset<TPreset>() where TPreset : VoicePreset
        {
            return VoicePresets.FirstOrDefault(p => p.Id == FemaleVoicePresetId && p.EnabledBackend == Backend) as TPreset;
        }

        public bool TryCreateVoicePreset<TPreset>(out TPreset preset) where TPreset : VoicePreset, new()
        {
            var highestId = VoicePresets.Select(p => p.Id).Max();
            preset = new TPreset
            {
                Id = highestId + 1,
                Name = "New preset",
                EnabledBackend = TTSBackend.System,
            };

            if (preset.TrySetDefaultValues())
            {
                VoicePresets.Add(preset);
                return true;
            }

            return false;
        }

        public void SetCurrentVoicePreset(int presetId)
        {
            CurrentVoicePresetId = presetId;
        }
    }
}