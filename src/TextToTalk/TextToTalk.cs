using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LiteDB;
using Lumina.Excel.Sheets;
using NAudio.Wave;
using R3;
using Standart.Hash.xxHash;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using TextToTalk.Backends;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.ElevenLabs;
using TextToTalk.Backends.GoogleCloud;
using TextToTalk.Backends.Kokoro;
using TextToTalk.Backends.OpenAI;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;
using TextToTalk.CommandModules;
using TextToTalk.Data.Services;
using TextToTalk.Events;
using TextToTalk.Extensions;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Services;
using TextToTalk.Talk;
using TextToTalk.TextProviders;
using TextToTalk.UI;
using TextToTalk.UngenderedOverrides;
using TextToTalk.Utils;
using GameObject = Dalamud.Game.ClientState.Objects.Types.IGameObject;

namespace TextToTalk
{
    public partial class TextToTalk : IDalamudPlugin
    {
#if DEBUG
        private const bool InitiallyVisible = true;
#else
        private const bool InitiallyVisible = false;
#endif

        private readonly IDalamudPluginInterface pluginInterface;
        private readonly MainCommandModule commandModule;
        private readonly DebugCommandModule debugCommandModule;
        private readonly IKeyState keys;
        private readonly IChatGui chat;
        private readonly IFramework framework;
        private readonly IClientState clientState;

        private readonly PluginConfiguration config;
        private readonly AddonTalkManager addonTalkManager;
        private readonly AddonBattleTalkManager addonBattleTalkManager;
        private readonly VoiceBackendManager backendManager;
        private readonly IAddonTalkHandler addonTalkHandler;
        private readonly IAddonBattleTalkHandler addonBattleTalkHandler;
        private readonly IChatMessageHandler chatMessageHandler;
        private readonly SoundHandler soundHandler;
        private readonly ConfiguredRateLimiter rateLimiter;
        private readonly UngenderedOverrideManager ungenderedOverrides;
        private readonly ILiteDatabase database;
        private readonly PlayerService playerService;
        private readonly NpcService npcService;
        private readonly WindowSystem windows;
        private readonly IDataManager data;
        private readonly NotificationService notificationService;

        private readonly ConfigurationWindow configurationWindow;
        private readonly VoiceUnlockerWindow voiceUnlockerWindow;

        private readonly HttpClient http;

        private readonly IDisposable unsubscribeAll;

        private ILiteDatabase? textEventLogDatabase;
        private TextEventLogCollection? textEventLog;

        public string Name => "TextToTalk";


        public TextToTalk(
            IDalamudPluginInterface pi,
            IKeyState keyState,
            IChatGui chat,
            IFramework framework,
            IClientState clientState,
            IGameGui gui,
            IDataManager data,
            IObjectTable objects,
            ICondition condition,
            ICommandManager commandManager,
            ISigScanner sigScanner,
            IGameInteropProvider gameInterop,
            INotificationManager notificationManager,
            IPluginLog pluginLog)
        {
            DetailedLog.SetLogger(pluginLog);

            this.pluginInterface = pi;
            this.clientState = clientState;
            this.keys = keyState;
            this.chat = chat;
            this.framework = framework;
            this.data = data;

            CreateDatabasePath();
            CreateEventLogDatabase();
            this.database = new LiteDatabase(GetDatabasePath("TextToTalk.db"));
            var playerCollection = new PlayerCollection(this.database);
            var npcCollection = new NpcCollection(this.database);

            this.notificationService = new NotificationService(notificationManager, chat, clientState);

            this.windows = new WindowSystem("TextToTalk");

            this.config = (PluginConfiguration?)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface, playerCollection, npcCollection);

            this.addonTalkManager = new AddonTalkManager(framework, clientState, condition, gui);
            this.addonBattleTalkManager = new AddonBattleTalkManager(framework, clientState, condition, gui);

            var sharedState = new SharedState();

            this.http = new HttpClient();
            this.backendManager =
                new VoiceBackendManager(this.config, this.http, pi.UiBuilder, this.notificationService);

            this.playerService = new PlayerService(playerCollection, this.config.GetVoiceConfig().VoicePresets);
            this.npcService = new NpcService(npcCollection, this.config.GetVoiceConfig().VoicePresets);

            var unlockerResultWindow = new UnlockerResultWindow();
            var channelPresetModificationWindow = new ChannelPresetModificationWindow(this.config);
            var voiceUnlockerRunner = new VoiceUnlockerRunner(pi.AssemblyLocation.FullName);
            this.voiceUnlockerWindow = new VoiceUnlockerWindow(voiceUnlockerRunner);
            var handleUnlockerResult = this.voiceUnlockerWindow.OnResult()
                .Subscribe(unlockerResultWindow, static (result, window) =>
                {
                    window.Text = result;
                    window.IsOpen = true;
                });
            this.configurationWindow = new ConfigurationWindow(this.config, data, this.backendManager,
                this.playerService, this.npcService, this.voiceUnlockerWindow)
            {
                IsOpen = InitiallyVisible,
            };
            var handlePresetOpenRequested = this.configurationWindow.OnPresetOpenRequested()
                .Subscribe(channelPresetModificationWindow, static (_, window) => window.IsOpen = true);

            this.windows.AddWindow(unlockerResultWindow);
            this.windows.AddWindow(this.voiceUnlockerWindow);
            this.windows.AddWindow(this.configurationWindow);
            this.windows.AddWindow(channelPresetModificationWindow);

            var filters = new MessageHandlerFilters(sharedState, this.config, this.clientState);
            this.addonTalkHandler =
                new AddonTalkHandler(this.addonTalkManager, framework, filters, objects, this.config);
            this.addonBattleTalkHandler =
                new AddonBattleTalkHandler(this.addonBattleTalkManager, framework, filters, objects, this.config);
            this.chatMessageHandler =
                new ChatMessageHandler(this.addonTalkManager, this.addonBattleTalkManager, chat, filters, objects,
                    this.config);
            this.soundHandler =
                new SoundHandler(this.addonTalkHandler, this.addonBattleTalkHandler, sigScanner, gameInterop);

            this.rateLimiter = new ConfiguredRateLimiter(this.config);

            this.ungenderedOverrides = new UngenderedOverrideManager();

            this.commandModule = new MainCommandModule(commandManager, chat, this.config, this.backendManager,
                this.configurationWindow);
            this.debugCommandModule = new DebugCommandModule(commandManager, chat, gui, framework);


            RegisterCallbacks();

            var handleTextCancel = HandleTextCancel();
            var handleTextEmit = HandleTextEmit();


            this.unsubscribeAll = Disposable.Combine(handleTextCancel, handleTextEmit, handleUnlockerResult,
                handlePresetOpenRequested);
        }


        private void CreateDatabasePath()
        {
            Directory.CreateDirectory(this.pluginInterface.GetPluginConfigDirectory());
        }

        private string GetDatabasePath(string fileName)
        {
            return Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), fileName);
        }

        [Conditional("DEBUG")]
        private void CreateEventLogDatabase()
        {
            this.textEventLogDatabase = new LiteDatabase(GetDatabasePath("log.db"));
            this.textEventLog = new TextEventLogCollection(this.textEventLogDatabase);
        }

        private IDisposable HandleTextCancel()
        {
            return OnTextSourceCancel()
                .Where(this, static (_, p) => p.config is { Enabled: true, CancelSpeechOnTextAdvance: true })
                .Do(LogTextEvent)
                .SubscribeOnThreadPool()
                .Subscribe(
                    ev => FunctionalUtils.RunSafely(
                        () => this.backendManager.CancelSay(ev.Source),
                        ex => DetailedLog.Error(ex, "Failed to handle text cancel event")),
                    ex => DetailedLog.Error(ex, "Text cancel event sequence has faulted"),
                    _ => { });
        }

        private IDisposable HandleTextEmit()
        {
            return OnTextEmit()
                .Where(this, static (_, p) => p.config.Enabled)
                .Do(LogTextEvent)
                .SubscribeOnThreadPool()
                .Subscribe(
                    ev => FunctionalUtils.RunSafely(
                        () => Say(ev.Speaker, ev.SpeakerName, ev.GetChatType(), ev.Text.TextValue, ev.Source),
                        ex => DetailedLog.Error(ex, "Failed to handle text emit event")),
                    ex => DetailedLog.Error(ex, "Text emit event sequence has faulted"),
                    _ => { });
        }

        private void LogTextEvent(TextEvent ev)
        {
            FunctionalUtils.RunSafely(
                () => this.textEventLog?.StoreEvent(ev.ToLogEntry()),
                ex => DetailedLog.Error(ex, "Failed to log text emit event"));
        }

        private bool keysDown;

        private void CheckKeybindPressed(IFramework f)
        {
            if (this.CheckTTSToggleKeybind()) return;
            if (this.CheckPresetKeybind()) return;
            this.keysDown = false;
        }

        private bool CheckPresetKeybind()
        {
            foreach (var preset in this.config.EnabledChatTypesPresets.Where(p => p.UseKeybind))
            {
                if (this.keys[(byte)preset.ModifierKey] &&
                    this.keys[(byte)preset.MajorKey])
                {
                    if (this.keysDown) return true;

                    this.keysDown = true;
                    this.config.SetCurrentEnabledChatTypesPreset(preset.Id);
                    this.chat.Print($"TextToTalk preset -> {preset.Name}");
                    DetailedLog.Info($"TextToTalk preset -> {preset.Name}");
                    return true;
                }
            }

            return false;
        }

        private bool CheckTTSToggleKeybind()
        {
            if (!this.config.UseKeybind)
            {
                return false;
            }

            if (this.keys[(byte)this.config.ModifierKey] &&
                this.keys[(byte)this.config.MajorKey])
            {
                if (this.keysDown) return true;

                this.keysDown = true;
                this.commandModule.ToggleTts();
                return true;
            }

            return false;
        }

        private void Say(GameObject? speaker, SeString speakerName, XivChatType? chatType, string textValue,
            TextSource source)
        {
            // Check if this speaker should be skipped
            if (speaker != null && this.rateLimiter.TryRateLimit(speaker))
            {
                return;
            }

            // Run a preprocessing pipeline to clean the text for the speech synthesizer
            var cleanText = FunctionalUtils.Pipe(
                textValue,
                TalkUtils.StripAngleBracketedText,
                TalkUtils.ReplaceSsmlTokens,
                TalkUtils.NormalizePunctuation,
                t => this.config.RemoveStutterEnabled ? TalkUtils.RemoveStutters(t) : t,
                x => x.Trim());

            // Ensure that the result is clean; ignore it otherwise
            if (!cleanText.Any() || !TalkUtils.IsSpeakable(cleanText))
            {
                return;
            }

            // Build a template for the text payload
            var textTemplate = TalkUtils.ExtractTokens(cleanText, new Dictionary<string, string?>
            {
                { "{{FULL_NAME}}", this.clientState.LocalPlayer?.GetFullName() },
                { "{{FIRST_NAME}}", this.clientState.LocalPlayer?.GetFirstName() },
                { "{{LAST_NAME}}", this.clientState.LocalPlayer?.GetLastName() },
            });

            // Some characters have emdashes in their names, which should be treated
            // as hyphens for the sake of the plugin.
            var cleanSpeakerName = TalkUtils.NormalizePunctuation(speakerName.TextValue);

            // Attempt to get the speaker's ID, if they're an NPC
            var npcId = speaker?.GetNpcId();

            // Get the speaker's race if it exists.
            var race = GetSpeakerRace(speaker);

            // Get the speaker's age if it exists.
            var bodyType = GetSpeakerBodyType(speaker);

            // Get the speaker's voice preset
            var preset = GetVoicePreset(speaker, cleanSpeakerName);
            if (preset is null)
            {
                DetailedLog.Error("Attempted to speak with null voice preset");
                return;
            }

            // Say the thing
            BackendSay(new SayRequest
            {
                Source = source,
                Speaker = cleanSpeakerName,
                Text = cleanText,
                TextTemplate = textTemplate,
                Voice = preset,
                ChatType = chatType,
                Language = this.clientState.ClientLanguage,
                NpcId = npcId,
                Race = race,
                BodyType = bodyType,
                StuttersRemoved = this.config.RemoveStutterEnabled,
            });
        }

        private void BackendSay(SayRequest request)
        {
            if (request.Voice.EnabledBackend != this.config.Backend)
            {
                DetailedLog.Error(
                    $"Voice preset {request.Voice.Name} is not compatible with the {this.config.Backend} backend");
                return;
            }

            this.backendManager.Say(request);
        }

        private unsafe string GetSpeakerRace(GameObject? speaker)
        {
            var race = this.data.GetExcelSheet<Race>();
            if (!TryGetCharacter(speaker, out var charaStruct))
            {
                return "Unknown";
            }

            var speakerRace = charaStruct->DrawData.CustomizeData.Race;

            if (!race.TryGetRow(speakerRace, out var row))
            {
                return "Unknown";
            }

            return row.Masculine.ToString();
        }

        private static unsafe BodyType GetSpeakerBodyType(GameObject? speaker)
        {
            if (!TryGetCharacter(speaker, out var charaStruct))
            {
                return BodyType.Unknown;
            }

            var speakerBodyType = charaStruct->DrawData.CustomizeData.BodyType;
            return (BodyType)speakerBodyType;
        }

        private static unsafe bool TryGetCharacter(GameObject? speaker,
            [NotNullWhen(true)] out FFXIVClientStructs.FFXIV.Client.Game.Character.Character* character)
        {
            character = null;
            if (speaker is null || speaker.Address == nint.Zero)
            {
                return false;
            }

            var objectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)speaker.Address;
            if (!objectStruct->IsCharacter())
            {
                return false;
            }

            character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)speaker.Address;
            return true;
        }

        private VoicePreset? GetVoicePreset(GameObject? speaker, string speakerName)
        {
            // Check if the speaker is a player and we have a custom voice for this speaker
            if (speaker is IPlayerCharacter pc &&
                this.config.UsePlayerVoicePresets &&
                this.playerService.TryGetPlayer(speakerName, pc.HomeWorld.RowId, out var playerInfo) &&
                this.playerService.TryGetPlayerVoice(playerInfo, out var playerVoice))
            {
                return playerVoice;
            }

            if (speaker is not null &&
                this.config.UseNpcVoicePresets &&
                this.npcService.TryGetNpc(speakerName, out var npcInfo) &&
                this.npcService.TryGetNpcVoice(npcInfo, out var npcVoice))
            {
                return npcVoice;
            }

            // Get the speaker's gender, if possible
            var gender = this.config.Backend == TTSBackend.Websocket || this.config.UseGenderedVoicePresets
                ? CharacterGenderUtils.GetCharacterGender(speaker, this.ungenderedOverrides)
                : Gender.None;

            return GetVoiceForSpeaker(speakerName, gender);
        }

        private VoicePreset? GetVoiceForSpeaker(string? name, Gender gender)
        {
            return this.backendManager.Backend switch
            {
                SystemBackend => GetVoiceForSpeaker<SystemVoicePreset>(name, gender),
                PollyBackend => GetVoiceForSpeaker<PollyVoicePreset>(name, gender),
                UberduckBackend => GetVoiceForSpeaker<UberduckVoicePreset>(name, gender),
                WebsocketBackend => new WebsocketVoicePreset
                {
                    EnabledBackend = TTSBackend.Websocket,
                    Id = -1,
                    Name = gender.ToString(),
                },
                AzureBackend => GetVoiceForSpeaker<AzureVoicePreset>(name, gender),
                ElevenLabsBackend => GetVoiceForSpeaker<ElevenLabsVoicePreset>(name, gender),
                OpenAiBackend => GetVoiceForSpeaker<OpenAiVoicePreset>(name, gender),
                GoogleCloudBackend => GetVoiceForSpeaker<GoogleCloudVoicePreset>(name, gender),
                KokoroBackend => GetVoiceForSpeaker<KokoroVoicePreset>(name, gender),
                _ => throw new InvalidOperationException("Failed to get voice preset for backend."),
            };
        }

        private TPreset? GetVoiceForSpeaker<TPreset>(string? name, Gender gender) where TPreset : VoicePreset
        {
            if (!this.config.UseGenderedVoicePresets)
            {
                return this.config.GetCurrentVoicePreset<TPreset>();
            }

            var voicePresets = gender switch
            {
                Gender.Male => this.config.GetCurrentMaleVoicePresets<TPreset>(),
                Gender.Female => this.config.GetCurrentFemaleVoicePresets<TPreset>(),
                _ => this.config.GetCurrentUngenderedVoicePresets<TPreset>(),
            };

            if (voicePresets.Length < 1)
            {
                return null;
            }

            // Use xxHash instead of the built-in GetHashCode because GetHashCode is randomized on program launch.
            var nameHash = string.IsNullOrEmpty(name) ? 0 : xxHash32.ComputeHash(name);
            var voicePresetIndex = (int)(nameHash % (uint)voicePresets.Length);
            return voicePresets[voicePresetIndex];
        }

        private void RegisterCallbacks()
        {
            this.pluginInterface.UiBuilder.Draw += this.windows.Draw;

            this.pluginInterface.UiBuilder.OpenMainUi += this.configurationWindow.Open;
            this.pluginInterface.UiBuilder.OpenConfigUi += this.configurationWindow.Open;

            this.framework.Update += this.notificationService.ProcessNotifications;
            this.framework.Update += CheckKeybindPressed;
        }

        private void UnregisterCallbacks()
        {
            this.framework.Update -= CheckKeybindPressed;
            this.framework.Update -= this.notificationService.ProcessNotifications;

            this.pluginInterface.UiBuilder.OpenConfigUi -= this.configurationWindow.Open;
            this.pluginInterface.UiBuilder.OpenMainUi -= this.configurationWindow.Open;

            this.pluginInterface.UiBuilder.Draw -= this.windows.Draw;
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.unsubscribeAll.Dispose();

            this.chatMessageHandler.Dispose();
            this.addonTalkHandler.Dispose();

            UnregisterCallbacks();

            this.debugCommandModule.Dispose();
            this.commandModule.Dispose();

            this.soundHandler.Dispose();

            this.configurationWindow.Dispose();

            this.voiceUnlockerWindow.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.backendManager.Dispose();
            this.http.Dispose();

            this.textEventLogDatabase?.Dispose();
            this.database.Dispose();

            this.addonBattleTalkManager.Dispose();
            this.addonTalkManager.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}