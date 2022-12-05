using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using Dalamud.Data;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Standart.Hash.xxHash;
using TextToTalk.Backends;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Modules;
using TextToTalk.Talk;
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
        private readonly VoiceBackendManager backendManager;
        private readonly TalkAddonHandler talkAddonHandler;
        private readonly ChatMessageHandler chatMessageHandler;
        private readonly RateLimiter rateLimiter;
        private readonly UngenderedOverrideManager ungenderedOverrides;
        private readonly PlayerService playerService;
        private readonly SharedState sharedState;
        private readonly WindowSystem windows;

        private readonly UnlockerResultWindow unlockerResultWindow;
        private readonly VoiceUnlockerWindow voiceUnlockerWindow;
        private readonly ConfigurationWindow configurationWindow;
        private readonly ChannelPresetModificationWindow channelPresetModificationWindow;

        private readonly HttpClient http;

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
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            this.pluginInterface = pi;
            this.clientState = clientState;
            this.keys = keyState;
            this.chat = chat;
            this.framework = framework;

            this.windows = new WindowSystem("TextToTalk");

            this.config = (PluginConfiguration)this.pluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.sharedState = new SharedState();

            this.http = new HttpClient();
            this.backendManager = new VoiceBackendManager(config, this.http, this.sharedState);

            this.playerService = new PlayerService(this.config.Players, this.config.PlayerVoicePresets,
                this.config.VoicePresetConfig.VoicePresets);

            this.unlockerResultWindow = new UnlockerResultWindow();
            this.channelPresetModificationWindow = new ChannelPresetModificationWindow(this.config);
            var windowController =
                new WindowController(this.unlockerResultWindow, this.channelPresetModificationWindow);
            this.voiceUnlockerWindow = new VoiceUnlockerWindow(windowController);
            this.configurationWindow = new ConfigurationWindow(this.config, data, this.backendManager,
                this.playerService, windowController, this.voiceUnlockerWindow)
            {
                IsOpen = InitiallyVisible,
            };

            this.windows.AddWindow(this.unlockerResultWindow);
            this.windows.AddWindow(this.voiceUnlockerWindow);
            this.windows.AddWindow(this.configurationWindow);
            this.windows.AddWindow(this.channelPresetModificationWindow);

            this.pluginInterface.UiBuilder.Draw += this.windows.Draw;

            var filters = new MessageHandlerFilters(this.sharedState, config);
            this.talkAddonHandler = new TalkAddonHandler(clientState, gui, data, filters, objects, this.config,
                this.sharedState, this.backendManager);
            this.talkAddonHandler.Say += Say;

            this.chatMessageHandler = new ChatMessageHandler(filters, objects, config, this.sharedState);
            this.chatMessageHandler.Say += Say;

            this.rateLimiter = new RateLimiter(() =>
            {
                if (config.MessagesPerSecond == 0)
                {
                    return long.MaxValue;
                }

                return (long)(1000f / config.MessagesPerSecond);
            });

            this.ungenderedOverrides = new UngenderedOverrideManager();

            pi.UiBuilder.OpenConfigUi += OpenConfigUi;

            this.chat.ChatMessage += OnChatMessage;
            this.chat.ChatMessage += CheckFailedToBindPort;

            this.framework.Update += PollTalkAddon;
            this.framework.Update += CheckKeybindPressed;

            this.commandModule = new MainCommandModule(this.chat, commandManager, this.config, this.backendManager,
                this.configurationWindow);
        }

        private bool keysDown = false;

        private void CheckKeybindPressed(Framework f)
        {
            if (!this.config.UseKeybind) return;

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
                    PluginLog.Log($"TextToTalk preset -> {preset.Name}");
                    return true;
                }
            }

            return false;
        }

        private bool CheckTTSToggleKeybind()
        {
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

        private void PollTalkAddon(Framework f)
        {
            if (!this.config.Enabled) return;
            if (!this.config.ReadFromQuestTalkAddon) return;
            talkAddonHandler.PollAddon(framework);
        }

        private bool notifiedFailedToBindPort;

        private void CheckFailedToBindPort(XivChatType type, uint id, ref SeString sender, ref SeString message,
            ref bool handled)
        {
            if (!clientState.IsLoggedIn || !sharedState.WSFailedToBindPort || this.notifiedFailedToBindPort) return;
            chat.Print($"TextToTalk failed to bind to port {config.WebsocketPort}. " +
                       "Please close the owner of that port and reload the Websocket server, " +
                       "or select a different port.");
            this.notifiedFailedToBindPort = true;
        }

        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message,
            ref bool handled)
        {
            if (!this.config.Enabled) return;
            chatMessageHandler.ProcessMessage(type, id, ref sender, ref message, ref handled);
        }

        private void Say(GameObject speaker, string textValue, TextSource source)
        {
            // Check if this speaker should be skipped
            if (ShouldRateLimit(speaker))
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
                playerService.TryGetPlayerByInfo(pc.Name.TextValue, pc.HomeWorld.Id, out var playerInfo) &&
                playerService.TryGetPlayerVoice(playerInfo, out var voice))
            {
                if (voice.EnabledBackend != this.config.Backend)
                {
                    PluginLog.LogError(
                        $"Voice preset {voice.Name} is not compatible with the {this.config.Backend} backend");
                }
                else
                {
                    backendManager.Say(source, voice, cleanText);
                }
            }
            else
            {
                // Get the speaker's gender, if possible
                var gender = this.config.UseGenderedVoicePresets ? GetCharacterGender(speaker) : Gender.None;

                // Say the thing
                var preset = GetVoiceForSpeaker(speaker.Name.TextValue, gender);
                if (preset != null)
                {
                    backendManager.Say(source, preset, cleanText);
                }
                else
                {
                    PluginLog.LogError("Attempted to speak with null voice preset");
                }
            }
        }

        private VoicePreset GetVoiceForSpeaker(string name, Gender gender)
        {
            return backendManager.Backend switch
            {
                SystemBackend => GetVoiceForSpeaker<SystemVoicePreset>(name, gender),
                PollyBackend => GetVoiceForSpeaker<PollyVoicePreset>(name, gender),
                UberduckBackend => GetVoiceForSpeaker<UberduckVoicePreset>(name, gender),
                WebsocketBackend => GetVoiceForSpeaker<WebsocketVoicePreset>(name, gender),
                _ => throw new InvalidOperationException("Failed to get voice preset for backend."),
            };
        }

        private TPreset GetVoiceForSpeaker<TPreset>(string name, Gender gender) where TPreset : VoicePreset
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
            var nameHash = xxHash32.ComputeHash(name);
            var voicePresetIndex = (int)(nameHash % (uint)voicePresets.Length);
            return voicePresets[voicePresetIndex];
        }

        private bool ShouldRateLimit(GameObject speaker)
        {
            return this.config.UsePlayerRateLimiter &&
                   speaker.ObjectKind is ObjectKind.Player &&
                   rateLimiter.TryRateLimit(speaker.Name.TextValue);
        }

        private unsafe Gender GetCharacterGender(GameObject gObj)
        {
            if (gObj == null)
            {
                PluginLog.Log("GameObject is null; cannot check gender.");
                return Gender.None;
            }

            var charaStruct = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)gObj.Address;
            if (charaStruct == null)
            {
                PluginLog.Warning("Failed to retrieve character struct.");
                return Gender.None;
            }

            // Get actor gender as defined by its struct.
            var actorGender = (Gender)charaStruct->CustomizeData[1];

            // Player gender overrides will be handled by a different system.
            if (gObj.ObjectKind is ObjectKind.Player)
            {
                return actorGender;
            }

            // Get the actor's model ID to see if we have an ungendered override for it.
            // Actors only have 0/1 genders regardless of their canonical genders, so this
            // needs to be specified by us. If an actor is canonically ungendered, their
            // gender seems to always be left at 0 (male).
            var modelId = Marshal.ReadInt32((IntPtr)charaStruct, 0x1BC);
            if (modelId == -1)
            {
                // https://github.com/aers/FFXIVClientStructs/blob/5e6b8ca2959f396b4d8c88253e4bc82fa6af54b7/FFXIVClientStructs/FFXIV/Client/Game/Character/Character.cs#L23
                modelId = Marshal.ReadInt32((IntPtr)charaStruct, 0x1B4);
            }

            // Get the override state and log the model ID so that we can add it to our overrides file if needed.
            if (ungenderedOverrides.IsUngendered(modelId))
            {
                actorGender = Gender.None;
                PluginLog.Log(
                    $"Got model ID {modelId} for {gObj.ObjectKind} \"{gObj.Name}\" (gender overriden to: {actorGender})");
            }
            else
            {
                PluginLog.Log(
                    $"Got model ID {modelId} for {gObj.ObjectKind} \"{gObj.Name}\" (gender read as: {actorGender})");
            }

            return actorGender;
        }

        private void OpenConfigUi()
        {
            this.configurationWindow.IsOpen = true;
        }

        private static T Pipe<T>(T input, params Func<T, T>[] transforms)
        {
            return transforms.Aggregate(input, (agg, next) => next(agg));
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.windows.RemoveWindow(this.unlockerResultWindow);
            this.windows.RemoveWindow(this.voiceUnlockerWindow);
            this.windows.RemoveWindow(this.configurationWindow);
            this.windows.RemoveWindow(this.channelPresetModificationWindow);
            
            this.pluginInterface.UiBuilder.Draw -= this.windows.Draw;

            this.framework.Update -= PollTalkAddon;
            this.framework.Update -= CheckKeybindPressed;

            this.chat.ChatMessage -= CheckFailedToBindPort;
            this.chat.ChatMessage -= OnChatMessage;

            this.pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

            this.pluginInterface.SavePluginConfig(this.config);

            this.backendManager.Dispose();
            this.http.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}