using Amazon.Polly;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TextToTalk.Backends;
using TextToTalk.Backends.System;
using TextToTalk.Data.Service;
using TextToTalk.Migrations;
using TextToTalk.UI.Core;

// ReSharper disable InconsistentNaming

namespace TextToTalk
{
    public enum FirstOrLastName
    {
        First,
        Last,
    }

    public class PluginConfiguration : IPluginConfiguration, ISaveable
    {
        private const string DefaultPreset = "Default";

        #region Obsolete Members

        [Obsolete("Use EnabledChatTypesPresets.")]
        public bool EnableAllChatTypes { get; set; }

        [Obsolete("Use EnabledChatTypesPresets.")]
        // ReSharper disable once CollectionNeverUpdated.Global
        public IList<int> EnabledChatTypes { get; set; }

        // ReSharper disable once CollectionNeverUpdated.Global
        [Obsolete] public IList<VoicePreset> VoicePresets { get; set; }

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

        [Obsolete] public string? UberduckVoice { get; set; } = "zwf";

        [Obsolete] public string? UberduckVoiceUngendered { get; set; } = "zwf";

        [Obsolete] public string? UberduckVoiceMale { get; set; } = "zwf";

        [Obsolete] public string? UberduckVoiceFemale { get; set; } = "zwf";

        [Obsolete] public float UberduckVolume { get; set; } = 1.0f;

        [Obsolete] public int UberduckPlaybackRate { get; set; } = 100;

        [Obsolete] public int CurrentVoicePresetId { get; set; }
        [Obsolete] public int UngenderedVoicePresetId { get; set; }
        [Obsolete] public int MaleVoicePresetId { get; set; }
        [Obsolete] public int FemaleVoicePresetId { get; set; }

        // ReSharper disable once CollectionNeverUpdated.Global
        [Obsolete] public IDictionary<Guid, dynamic> Players { get; set; }

        // ReSharper disable once CollectionNeverUpdated.Global
        [Obsolete] public IDictionary<Guid, dynamic> Npcs { get; set; }

        // ReSharper disable once CollectionNeverUpdated.Global
        [Obsolete] public IDictionary<Guid, int> PlayerVoicePresets { get; set; }

        // ReSharper disable once CollectionNeverUpdated.Global
        [Obsolete] public IDictionary<Guid, int> NpcVoicePresets { get; set; }

        #endregion

        public int Version { get; set; }

        public bool Enabled { get; set; }

        public bool UseKeybind { get; set; }
        public VirtualKey.Enum ModifierKey { get; set; }
        public VirtualKey.Enum MajorKey { get; set; }

        public bool MigratedTo1_5 { get; set; }
        public bool MigratedTo1_6 { get; set; }
        public bool MigratedTo1_17 { get; set; }
        public bool MigratedTo1_18_2 { get; set; }
        public bool MigratedTo1_18_3 { get; set; }
        public bool MigratedTo1_25_0 { get; set; }

        public IList<Trigger> Bad { get; set; }
        public IList<Trigger> Good { get; set; }

        public int CurrentPresetId { get; set; }
        public IList<EnabledChatTypesPreset> EnabledChatTypesPresets { get; set; }

        public int WebsocketPort { get; set; }

        public bool NameNpcWithSay { get; set; } = true;
        public bool EnableNameWithSay { get; set; } = true;
        public bool DisallowMultipleSay { get; set; }
        public bool SayPlayerWorldName { get; set; } = true;
        public bool SayPartialName { get; set; }
        public FirstOrLastName OnlySayFirstOrLastName { get; set; } = FirstOrLastName.First;

        public bool ReadFromQuestTalkAddon { get; set; } = true;
        public bool CancelSpeechOnTextAdvance { get; set; }
        public bool SkipVoicedQuestText { get; set; } = true;

        public bool ReadFromBattleTalkAddon { get; set; } = true;
        public bool SkipVoicedBattleText { get; set; } = true;

        public bool UseGenderedVoicePresets { get; set; }

        public TTSBackend Backend { get; set; }

        public IList<string> Lexicons { get; set; }

        public string PollyRegion { get; set; }
        public IList<string> PollyLexiconFiles { get; set; }

        public IList<string> AzureLexiconFiles { get; set; }

        public bool RemoveStutterEnabled { get; set; } = true;

        public IDictionary<string, IDictionary<TTSBackend, bool>> RemoteLexiconEnabledBackends { get; set; }

        public bool UsePlayerRateLimiter { get; set; }
        public float MessagesPerSecond { get; set; } = 5;

        public bool SkipMessagesFromYou { get; set; }

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

        [JsonIgnore] private VoicePresetConfiguration voicePresetConfig;

        public PluginConfiguration()
        {
            Enabled = true;

            Bad = new List<Trigger>();
            Good = new List<Trigger>();

            ModifierKey = VirtualKey.Enum.VkControl;
            MajorKey = VirtualKey.Enum.VkN;
        }

        public void Initialize(
            DalamudPluginInterface pi,
            PlayerCollection playerCollection,
            NpcCollection npcCollection)
        {
            this.pluginInterface = pi;
            this.cfgLock = true;

            this.voicePresetConfig = VoicePresetConfiguration.LoadFromFile(GetVoicePresetsConfigPath());

            EnabledChatTypesPresets ??= new List<EnabledChatTypesPreset>();

            AzureLexiconFiles ??= new List<string>();
            PollyLexiconFiles ??= new List<string>();
            Lexicons ??= new List<string>();

            RemoteLexiconEnabledBackends ??= new Dictionary<string, IDictionary<TTSBackend, bool>>();

            PlayerVoicePresets ??= new Dictionary<Guid, int>();

            Npcs ??= new Dictionary<Guid, dynamic>();
            NpcVoicePresets ??= new Dictionary<Guid, int>();

            if (!InitializedEver)
            {
                EnabledChatTypesPresets.Add(new EnabledChatTypesPreset(this)
                {
                    Id = 0,
                    EnabledChatTypes = new List<int>
                    {
                        (int)XivChatType.Say,
                        (int)XivChatType.Yell,
                        (int)XivChatType.Shout,
                        (int)XivChatType.Party,
                        (int)XivChatType.NPCDialogue,
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
                        GetVoiceConfig().VoicePresets.Add(defaultPreset);
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to set values for default preset.");
                    }
                }
                catch (Exception e)
                {
                    DetailedLog.Error(e, "Failed to create default voice preset.");
                }

                InitializedEver = true;
                MigratedTo1_5 = true;
                MigratedTo1_6 = true;
                MigratedTo1_17 = true;
                MigratedTo1_18_2 = true;
                MigratedTo1_18_3 = true;
                MigratedTo1_25_0 = true;
            }

            if (InitializedEver)
            {
                var migrations = new IConfigurationMigration[]
                {
                    new Migration1_5(), new Migration1_6(), new Migration1_17(), new Migration1_18_2(),
                    new Migration1_18_3(), new Migration1_25_0(playerCollection, npcCollection),
                };
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

        public VoicePresetConfiguration GetVoiceConfig()
        {
            return this.voicePresetConfig;
        }

        public void Save()
        {
            lock (this.cfgLock)
            {
                this.pluginInterface.SavePluginConfig(this);
                GetVoiceConfig().SaveToFile(GetVoicePresetsConfigPath());
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
            var preset = new EnabledChatTypesPreset(this)
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
            return GetVoiceConfig().VoicePresets.Where(p => p.EnabledBackend == backend);
        }

        public void SetCurrentEnabledChatTypesPreset(int presetId)
        {
            CurrentPresetId = presetId;
        }

        public TPreset? GetCurrentVoicePreset<TPreset>() where TPreset : VoicePreset
        {
            var voiceConfig = GetVoiceConfig();
            return voiceConfig.VoicePresets.FirstOrDefault(p =>
                voiceConfig.CurrentVoicePreset.TryGetValue(Backend, out var id) && p.Id == id &&
                p.EnabledBackend == Backend) as TPreset;
        }

        public TPreset[]? GetCurrentUngenderedVoicePresets<TPreset>() where TPreset : VoicePreset
        {
            var voiceConfig = GetVoiceConfig();
            var presets = voiceConfig.GetUngenderedPresets(Backend);
            return voiceConfig.VoicePresets.Where(p =>
                    presets.Contains(p.Id) && p.EnabledBackend == Backend)
                .Cast<TPreset>().ToArray();
        }

        public TPreset[]? GetCurrentMaleVoicePresets<TPreset>() where TPreset : VoicePreset
        {
            var voiceConfig = GetVoiceConfig();
            var presets = voiceConfig.GetMalePresets(Backend);
            return voiceConfig.VoicePresets.Where(p =>
                    presets.Contains(p.Id) && p.EnabledBackend == Backend)
                .Cast<TPreset>().ToArray();
        }

        public TPreset[]? GetCurrentFemaleVoicePresets<TPreset>() where TPreset : VoicePreset
        {
            var voiceConfig = GetVoiceConfig();
            var presets = voiceConfig.GetFemalePresets(Backend);
            return voiceConfig.VoicePresets.Where(p =>
                    presets.Contains(p.Id) && p.EnabledBackend == Backend)
                .Cast<TPreset>().ToArray();
        }

        public int GetHighestVoicePresetId()
        {
            var voiceConfig = GetVoiceConfig();
            return voiceConfig.VoicePresets.Count == 0
                ? 0
                : voiceConfig.VoicePresets.Select(p => p.Id).Max();
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
                GetVoiceConfig().VoicePresets.Add(preset);
                return true;
            }

            return false;
        }

        public void SetCurrentVoicePreset(int presetId)
        {
            GetVoiceConfig().CurrentVoicePreset[Backend] = presetId;
        }
    }
}