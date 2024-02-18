using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using LiteDB;
using R3;
using Standart.Hash.xxHash;
using TextToTalk.Backends;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.ElevenLabs;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;
using TextToTalk.CommandModules;
using TextToTalk.Data.Service;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Talk;
using TextToTalk.TextProviders;
using TextToTalk.UI;
using TextToTalk.UngenderedOverrides;
using TextToTalk.Utils;
using GameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;

namespace TextToTalk
{
    public partial class TextToTalk : IDalamudPlugin
    {
#if DEBUG
        private const bool InitiallyVisible = true;
#else
        private const bool InitiallyVisible = false;
#endif

        private readonly DalamudPluginInterface pluginInterface;
        private readonly MainCommandModule commandModule;
        private readonly IKeyState keys;
        private readonly IChatGui chat;
        private readonly IFramework framework;
        private readonly IClientState clientState;

        private readonly PluginConfiguration config;
        private readonly AddonTalkManager addonTalkManager;
        private readonly AddonBattleTalkManager addonBattleTalkManager;
        private readonly VoiceBackendManager backendManager;
        private readonly AddonTalkHandler addonTalkHandler;
        private readonly AddonBattleTalkHandler addonBattleTalkHandler;
        private readonly ChatMessageHandler chatMessageHandler;
        private readonly SoundHandler soundHandler;
        private readonly RateLimiter rateLimiter;
        private readonly UngenderedOverrideManager ungenderedOverrides;
        private readonly ILiteDatabase database;
        private readonly PlayerService playerService;
        private readonly NpcService npcService;
        private readonly WindowSystem windows;

        private readonly ConfigurationWindow configurationWindow;
        private readonly VoiceUnlockerWindow voiceUnlockerWindow;

        private readonly HttpClient http;

        private readonly IDisposable handleTextCancel;
        private readonly IDisposable handleTextEmit;
        private readonly IDisposable handleUnlockerResult;
        private readonly IDisposable handlePresetOpenRequested;
        private readonly IDisposable handleFailedToBindWsPort;

        private bool failedToBindWsPort;
        private bool notifiedFailedToBindPort;
        private bool notifiedNoPresetsConfigured;

        public string Name => "TextToTalk";

        public TextToTalk(
            [RequiredVersion("1.0")] DalamudPluginInterface pi,
            [RequiredVersion("1.0")] IKeyState keyState,
            [RequiredVersion("1.0")] IChatGui chat,
            [RequiredVersion("1.0")] IFramework framework,
            [RequiredVersion("1.0")] IClientState clientState,
            [RequiredVersion("1.0")] IGameGui gui,
            [RequiredVersion("1.0")] IDataManager data,
            [RequiredVersion("1.0")] IObjectTable objects,
            [RequiredVersion("1.0")] ICondition condition,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            [RequiredVersion("1.0")] ISigScanner sigScanner,
            [RequiredVersion("1.0")] IGameInteropProvider gameInterop)
        {
            this.pluginInterface = pi;
            this.clientState = clientState;
            this.keys = keyState;
            this.chat = chat;
            this.framework = framework;

            CreateDatabasePath();
            this.database = new LiteDatabase(GetDatabasePath("TextToTalk.db"));
            var playerCollection = new PlayerCollection(this.database);
            var npcCollection = new NpcCollection(this.database);

            this.windows = new WindowSystem("TextToTalk");

            this.config = (PluginConfiguration?)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface, playerCollection, npcCollection);

            this.addonTalkManager = new AddonTalkManager(framework, clientState, condition, gui);
            this.addonBattleTalkManager = new AddonBattleTalkManager(framework, clientState, condition, gui);

            var sharedState = new SharedState();

            this.http = new HttpClient();
            this.backendManager = new VoiceBackendManager(this.config, this.http, this.pluginInterface.UiBuilder);
            this.handleFailedToBindWsPort = HandleFailedToBindWSPort();

            this.playerService = new PlayerService(playerCollection, this.config.GetVoiceConfig().VoicePresets);
            this.npcService = new NpcService(npcCollection, this.config.GetVoiceConfig().VoicePresets);

            var unlockerResultWindow = new UnlockerResultWindow();
            var channelPresetModificationWindow = new ChannelPresetModificationWindow(this.config);
            this.voiceUnlockerWindow = new VoiceUnlockerWindow();
            this.handleUnlockerResult = this.voiceUnlockerWindow.OnResult()
                .Subscribe(result =>
                {
                    unlockerResultWindow.Text = result;
                    unlockerResultWindow.IsOpen = true;
                });
            this.configurationWindow = new ConfigurationWindow(this.config, data, this.backendManager,
                this.playerService, this.npcService, this.voiceUnlockerWindow)
            {
                IsOpen = InitiallyVisible,
            };
            this.handlePresetOpenRequested = this.configurationWindow.OnPresetOpenRequested()
                .Subscribe(_ => channelPresetModificationWindow.IsOpen = true);

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

            this.rateLimiter = new RateLimiter(() =>
            {
                if (this.config.MessagesPerSecond == 0)
                {
                    return long.MaxValue;
                }

                return (long)(1000f / this.config.MessagesPerSecond);
            });

            this.ungenderedOverrides = new UngenderedOverrideManager();

            this.commandModule = new MainCommandModule(this.chat, commandManager, this.config, this.backendManager,
                this.configurationWindow, gui);

            RegisterCallbacks();

            this.handleTextCancel = HandleTextCancel();
            this.handleTextEmit = HandleTextEmit();
        }

        private void CreateDatabasePath()
        {
            Directory.CreateDirectory(this.pluginInterface.GetPluginConfigDirectory());
        }

        private string GetDatabasePath(string fileName)
        {
            return Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), fileName);
        }

        private IDisposable HandleTextCancel()
        {
            return OnTextSourceCancel()
                .Where(this, static (_, p) => p.config is { Enabled: true, CancelSpeechOnTextAdvance: true })
                .SubscribeOnThreadPool()
                .Subscribe(
                    ev => FunctionalUtils.RunSafely(
                        () => this.backendManager.CancelSay(ev.Source),
                        ex => DetailedLog.Error(ex, "Failed to handle text cancel event")),
                    ex => DetailedLog.Error(ex, "Text cancel event sequence has faulted"),
                    _ => {});
        }

        private IDisposable HandleTextEmit()
        {
            return OnTextEmit()
                .Where(this, static (_, p) => p.config.Enabled)
                .SubscribeOnThreadPool()
                .Subscribe(
                    ev => FunctionalUtils.RunSafely(
                        () => Say(ev.Speaker, ev.SpeakerName, ev.Text.TextValue, ev.Source),
                        ex => DetailedLog.Error(ex, "Failed to handle text emit event")),
                    ex => DetailedLog.Error(ex, "Text emit event sequence has faulted"),
                    _ => {});
        }

        private IDisposable HandleFailedToBindWSPort()
        {
            return this.backendManager.OnFailedToBindWSPort()
                .Subscribe(this, static (_, p) =>
                {
                    p.failedToBindWsPort = true;
                    p.notifiedFailedToBindPort = false;
                });
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

        private void CheckFailedToBindPort(XivChatType type, uint id, ref SeString sender, ref SeString message,
            ref bool handled)
        {
            if (!this.clientState.IsLoggedIn || !this.failedToBindWsPort ||
                this.notifiedFailedToBindPort) return;
            this.chat.Print($"TextToTalk failed to bind to port {this.config.WebsocketPort}. " +
                            "Please close the owner of that port and reload the Websocket server, " +
                            "or select a different port.");
            this.notifiedFailedToBindPort = true;
        }

        private void WarnIfNoPresetsConfiguredForBackend(XivChatType type, uint id, ref SeString sender,
            ref SeString message, ref bool handled)
        {
            if (!this.clientState.IsLoggedIn || this.notifiedNoPresetsConfigured) return;
            if (this.config.Enabled &&
                this.config.GetVoiceConfig().VoicePresets.All(vp => vp.EnabledBackend != this.config.Backend))
            {
                FunctionalUtils.RunSafely(
                    () => this.chat.Print(
                        "You have no voice presets configured. Please create a voice preset in the TextToTalk configuration."),
                    ex => DetailedLog.Error(ex, "Failed to print chat message."));
            }

            this.notifiedNoPresetsConfigured = true;
        }

        private void Say(GameObject? speaker, SeString speakerName, string textValue, TextSource source)
        {
            // Check if this speaker should be skipped
            if (speaker != null && ShouldRateLimit(speaker))
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

            // Some characters have emdashes in their names, which should be treated
            // as hyphens for the sake of the plugin.
            var cleanSpeakerName = TalkUtils.NormalizePunctuation(speakerName.TextValue);

            // Get the speaker's voice preset
            var preset = GetVoicePreset(speaker, cleanSpeakerName);

            // Say the thing
            BackendSay(source, preset, cleanSpeakerName, cleanText);
        }

        private VoicePreset? GetVoicePreset(GameObject? speaker, string speakerName)
        {
            // Check if the speaker is a player and we have a custom voice for this speaker
            if (speaker is PlayerCharacter pc &&
                this.config.UsePlayerVoicePresets &&
                this.playerService.TryGetPlayer(speakerName, pc.HomeWorld.Id, out var playerInfo) &&
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
            var gender = this.config.UseGenderedVoicePresets
                ? CharacterGenderUtils.GetCharacterGender(speaker, this.ungenderedOverrides)
                : Gender.None;

            return GetVoiceForSpeaker(speakerName, gender);
        }

        private void BackendSay(TextSource source, VoicePreset? voicePreset, string speaker, string text)
        {
            if (voicePreset is null)
            {
                DetailedLog.Error("Attempted to speak with null voice preset");
                return;
            }

            if (voicePreset.EnabledBackend != this.config.Backend)
            {
                DetailedLog.Error(
                    $"Voice preset {voicePreset.Name} is not compatible with the {this.config.Backend} backend");
                return;
            }

            this.backendManager.Say(source, voicePreset, speaker, text);
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

            if (voicePresets == null || voicePresets.Length < 1)
            {
                return null;
            }

            // Use xxHash instead of the built-in GetHashCode because GetHashCode is randomized on program launch.
            var nameHash = string.IsNullOrEmpty(name) ? 0 : xxHash32.ComputeHash(name);
            var voicePresetIndex = (int)(nameHash % (uint)voicePresets.Length);
            return voicePresets[voicePresetIndex];
        }

        private bool ShouldRateLimit(GameObject speaker)
        {
            return this.config.UsePlayerRateLimiter &&
                   speaker.ObjectKind is ObjectKind.Player &&
                   this.rateLimiter.TryRateLimit(speaker.Name.TextValue);
        }

        private void OpenConfigUi()
        {
            this.configurationWindow.IsOpen = true;
        }

        private void RegisterCallbacks()
        {
            this.pluginInterface.UiBuilder.Draw += this.windows.Draw;

            this.pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            this.chat.ChatMessage += CheckFailedToBindPort;
            this.chat.ChatMessage += WarnIfNoPresetsConfiguredForBackend;

            this.framework.Update += CheckKeybindPressed;
        }

        private void UnregisterCallbacks()
        {
            this.framework.Update -= CheckKeybindPressed;

            this.chat.ChatMessage -= WarnIfNoPresetsConfiguredForBackend;
            this.chat.ChatMessage -= CheckFailedToBindPort;

            this.pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

            this.pluginInterface.UiBuilder.Draw -= this.windows.Draw;
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.handleFailedToBindWsPort.Dispose();

            this.handleTextEmit.Dispose();
            this.handleTextCancel.Dispose();

            this.chatMessageHandler.Dispose();
            this.addonTalkHandler.Dispose();

            UnregisterCallbacks();

            this.commandModule.Dispose();

            this.soundHandler.Dispose();

            this.handlePresetOpenRequested.Dispose();
            this.configurationWindow.Dispose();

            this.handleUnlockerResult.Dispose();
            this.voiceUnlockerWindow.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.backendManager.Dispose();
            this.http.Dispose();

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