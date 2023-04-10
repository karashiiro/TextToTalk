using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Dalamud.Data;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Standart.Hash.xxHash;
using TextToTalk.Backends;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;
using TextToTalk.CommandModules;
using TextToTalk.Events;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Talk;
using TextToTalk.TextProviders;
using TextToTalk.UI;
using TextToTalk.UngenderedOverrides;
using GameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;

namespace TextToTalk
{
    public class TextToTalk : IDalamudPlugin
    {
#if DEBUG
        private const bool InitiallyVisible = true;
#else
        private const bool InitiallyVisible = false;
#endif

        private readonly DalamudPluginInterface pluginInterface;
        private readonly MainCommandModule commandModule;
        private readonly KeyState keys;
        private readonly ChatGui chat;
        private readonly Framework framework;
        private readonly ClientState clientState;

        private readonly PluginConfiguration config;
        private readonly AddonTalkManager addonTalkManager;
        private readonly VoiceBackendManager backendManager;
        private readonly AddonTalkHandler addonTalkHandler;
        private readonly ChatMessageHandler chatMessageHandler;
        private readonly SoundHandler soundHandler;
        private readonly RateLimiter rateLimiter;
        private readonly UngenderedOverrideManager ungenderedOverrides;
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
            [RequiredVersion("1.0")] KeyState keyState,
            [RequiredVersion("1.0")] ChatGui chat,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] GameGui gui,
            [RequiredVersion("1.0")] DataManager data,
            [RequiredVersion("1.0")] ObjectTable objects,
            [RequiredVersion("1.0")] Condition condition,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] SigScanner sigScanner)
        {
            this.pluginInterface = pi;
            this.clientState = clientState;
            this.keys = keyState;
            this.chat = chat;
            this.framework = framework;

            this.windows = new WindowSystem("TextToTalk");

            this.config = (PluginConfiguration?)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.addonTalkManager = new AddonTalkManager(framework, clientState, condition, data, gui);

            var sharedState = new SharedState();

            this.http = new HttpClient();
            this.backendManager = new VoiceBackendManager(this.config, this.http);
            this.handleFailedToBindWsPort = HandleFailedToBindWSPort();

            this.playerService = new PlayerService(this.config.Players, this.config.PlayerVoicePresets,
                this.config.GetVoiceConfig().VoicePresets);
            this.npcService = new NpcService(this.config.Npcs, this.config.NpcVoicePresets,
                this.config.GetVoiceConfig().VoicePresets);

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
            this.chatMessageHandler =
                new ChatMessageHandler(this.addonTalkManager, chat, filters, objects, this.config);
            this.soundHandler = new SoundHandler(this.addonTalkHandler, sigScanner);

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
                this.configurationWindow);

            RegisterCallbacks();

            this.handleTextCancel = HandleTextCancel();
            this.handleTextEmit = HandleTextEmit();
        }

        private IDisposable HandleTextCancel()
        {
            return OnTextSourceCancel()
                .Where(_ => this.config is { Enabled: true, CancelSpeechOnTextAdvance: true })
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                    ev => this.backendManager.CancelSay(ev.Source),
                    ex => DetailedLog.Error(ex, "Failed to handle text cancel event"));
        }

        private IDisposable HandleTextEmit()
        {
            return OnTextEmit()
                .Where(_ => this.config.Enabled)
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(
                    ev => Say(ev.Speaker, ev.Text.TextValue, ev.Source),
                    ex => DetailedLog.Error(ex, "Failed to handle text emit event"));
        }

        private IDisposable HandleFailedToBindWSPort()
        {
            return this.backendManager.OnFailedToBindWSPort()
                .Subscribe(_ =>
                {
                    this.failedToBindWsPort = true;
                    this.notifiedFailedToBindPort = false;
                });
        }

        private bool keysDown;

        private void CheckKeybindPressed(Framework f)
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
                try
                {
                    this.chat.Print(
                        "You have no voice presets configured. Please create a voice preset in the TextToTalk configuration.");
                }
                catch (Exception e)
                {
                    DetailedLog.Error(e, "Failed to print chat message.");
                }
            }

            this.notifiedNoPresetsConfigured = true;
        }

        private void Say(GameObject? speaker, string textValue, TextSource source)
        {
            // Check if this speaker should be skipped
            if (speaker != null && ShouldRateLimit(speaker))
            {
                return;
            }

            // Run a preprocessing pipeline to clean the text for the speech synthesizer
            var cleanText = Pipe(
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

            // Check if the speaker is a player and we have a custom voice for this speaker
            if (speaker is PlayerCharacter pc &&
                this.playerService.TryGetPlayerByInfo(pc.Name.TextValue, pc.HomeWorld.Id, out var playerInfo) &&
                this.playerService.TryGetPlayerVoice(playerInfo, out var playerVoice))
            {
                if (playerVoice.EnabledBackend != this.config.Backend)
                {
                    DetailedLog.Error(
                        $"Voice preset {playerVoice.Name} is not compatible with the {this.config.Backend} backend");
                }
                else
                {
                    this.backendManager.Say(source, playerVoice, pc.Name.TextValue, cleanText);
                }
            }
            else if (speaker is not null &&
                     // Some characters have emdashes in their names, which should be treated
                     // as hyphens for the sake of the plugin.
                     this.npcService.TryGetNpcByInfo(TalkUtils.NormalizePunctuation(speaker.Name.TextValue),
                         out var npcInfo) &&
                     this.npcService.TryGetNpcVoice(npcInfo, out var npcVoice))
            {
                if (this.config.Backend != TTSBackend.Websocket && npcVoice.EnabledBackend != this.config.Backend)
                {
                    DetailedLog.Error(
                        $"Voice preset {npcVoice.Name} is not compatible with the {this.config.Backend} backend");
                }
                else
                {
                    this.backendManager.Say(source, npcVoice, speaker.Name.TextValue, cleanText);
                }
            }
            else
            {
                // Get the speaker's gender, if possible
                var gender = this.config.UseGenderedVoicePresets
                    ? CharacterGenderUtils.GetCharacterGender(speaker, this.ungenderedOverrides)
                    : Gender.None;

                // Say the thing
                var preset = GetVoiceForSpeaker(speaker?.Name.TextValue, gender);
                if (preset != null)
                {
                    this.backendManager.Say(source, preset, speaker?.Name.TextValue ?? "", cleanText);
                }
                else
                {
                    DetailedLog.Error("Attempted to speak with null voice preset");
                }
            }
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

        private static T Pipe<T>(T input, params Func<T, T>[] transforms)
        {
            return transforms.Aggregate(input, (agg, next) => next(agg));
        }

        #region IObservable Event Wrappers

        private IObservable<ChatTextEmitEvent> OnChatTextEmit()
        {
            return Observable.FromEvent<ChatTextEmitEvent>(
                    h => this.chatMessageHandler.OnTextEmit += h,
                    h => this.chatMessageHandler.OnTextEmit -= h)
                .Where(ev =>
                {
                    // Check all of the other filters to see if this should be dropped
                    var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();
                    var typeEnabled = chatTypes.EnabledChatTypes is not null &&
                                      chatTypes.EnabledChatTypes.Contains((int)ev.ChatType);
                    return chatTypes.EnableAllChatTypes || typeEnabled;
                })
                .Where(ev => !IsTextBad(ev.Text.TextValue))
                .Where(ev => IsTextGood(ev.Text.TextValue));
        }

        private IObservable<TextEmitEvent> OnTalkAddonTextEmit()
        {
            return Observable.FromEvent<TextEmitEvent>(
                h => this.addonTalkHandler.OnTextEmit += h,
                h => this.addonTalkHandler.OnTextEmit -= h);
        }

        private IObservable<AddonTalkAdvanceEvent> OnTalkAddonAdvance()
        {
            return Observable.FromEvent<AddonTalkAdvanceEvent>(
                h => this.addonTalkHandler.OnAdvance += h,
                h => this.addonTalkHandler.OnAdvance -= h);
        }

        private IObservable<AddonTalkCloseEvent> OnTalkAddonClose()
        {
            return Observable.FromEvent<AddonTalkCloseEvent>(
                h => this.addonTalkHandler.OnClose += h,
                h => this.addonTalkHandler.OnClose -= h);
        }

        private IObservable<SourcedTextEvent> OnTextSourceCancel()
        {
            return OnTalkAddonAdvance().Merge<SourcedTextEvent>(OnTalkAddonClose());
        }

        private IObservable<TextEmitEvent> OnTextEmit()
        {
            return OnTalkAddonTextEmit().Merge(OnChatTextEmit());
        }

        private bool IsTextGood(string text)
        {
            if (!this.config.Good.Any())
            {
                return true;
            }

            return this.config.Good
                .Where(t => t.Text != "")
                .Any(t => t.Match(text));
        }

        private bool IsTextBad(string text)
        {
            return this.config.Bad
                .Where(t => t.Text != "")
                .Any(t => t.Match(text));
        }

        #endregion

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