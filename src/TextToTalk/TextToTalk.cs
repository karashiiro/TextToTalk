using Dalamud.CrystalTower.Commands;
using Dalamud.CrystalTower.DependencyInjection;
using Dalamud.CrystalTower.UI;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using TextToTalk.Backends;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Modules;
using TextToTalk.Talk;
using TextToTalk.UI.Dalamud;
using TextToTalk.UngenderedOverrides;
using DalamudCommandManager = Dalamud.Game.Command.CommandManager;
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
        [PluginService]
        [RequiredVersion("1.0")]
        private DalamudPluginInterface PluginInterface { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private DalamudCommandManager Commands { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private ClientState ClientState { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private Framework Framework { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private DataManager Data { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private ChatGui Chat { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private GameGui Gui { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private KeyState Keys { get; init; }

        [PluginService]
        [RequiredVersion("1.0")]
        private ObjectTable Objects { get; init; }

        private readonly PluginConfiguration config;
        private readonly WindowManager ui;
        private readonly CommandManager commandManager;
        private readonly VoiceBackendManager backendManager;
        private readonly UngenderedOverrideManager ungenderedOverrides;
        private readonly RateLimiter rateLimiter;

        private IntPtr talkAddonPtr;
        private readonly SharedState sharedState;

        private readonly PluginServiceCollection serviceCollection;

        public string Name => "TextToTalk";

        public TextToTalk()
        {
            this.config = (PluginConfiguration)PluginInterface.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(PluginInterface);

            this.sharedState = new SharedState();
            this.backendManager = new VoiceBackendManager(this.config, this.sharedState);
            this.ungenderedOverrides = new UngenderedOverrideManager();
            this.rateLimiter = new RateLimiter(() =>
            {
                if (this.config.MessagesPerSecond == 0)
                {
                    return long.MaxValue;
                }

                return (long)(1f / this.config.MessagesPerSecond);
            });

            this.serviceCollection = new PluginServiceCollection();
            this.serviceCollection.AddService(this.config);
            this.serviceCollection.AddService(this.backendManager);
            this.serviceCollection.AddService(this.rateLimiter);
            this.serviceCollection.AddService(this.sharedState);
            this.serviceCollection.AddService(Chat, shouldDispose: false);
            this.serviceCollection.AddService(PluginInterface, shouldDispose: false);

            this.ui = new WindowManager(this.serviceCollection);
            this.serviceCollection.AddService(this.ui);

            this.ui.AddWindow<UnlockerResultWindow>(initiallyVisible: false);
            this.ui.AddWindow<VoiceUnlockerWindow>(initiallyVisible: false);
            this.ui.AddWindow<ChannelPresetModificationWindow>(initiallyVisible: false);
            this.ui.AddWindow<ConfigurationWindow>(InitiallyVisible);

            PluginInterface.UiBuilder.Draw += this.ui.Draw;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            Chat.ChatMessage += OnChatMessage;
            Chat.ChatMessage += CheckFailedToBindPort;

            Framework.Update += PollTalkAddon;
            Framework.Update += CheckKeybindPressed;
            Framework.Update += CheckPresetKeybindPressed;

            this.commandManager = new CommandManager(Commands, this.serviceCollection);
            this.commandManager.AddCommandModule<MainCommandModule>();
        }

        private bool keysDown;
        private void CheckKeybindPressed(Framework framework)
        {
            if (!this.config.UseKeybind) return;

            if (Keys[(byte)this.config.ModifierKey] &&
                Keys[(byte)this.config.MajorKey])
            {
                if (this.keysDown) return;

                this.keysDown = true;

                var commandModule = this.commandManager.GetCommandModule<MainCommandModule>();
                commandModule.ToggleTts();

                return;
            }

            this.keysDown = false;
        }

        private void CheckPresetKeybindPressed(Framework framework)
        {
            foreach (var preset in this.config.EnabledChatTypesPresets.Where(p => p.UseKeybind))
            {
                if (Keys[(byte)preset.ModifierKey] &&
                    Keys[(byte)preset.MajorKey])
                {
                    this.config.SetCurrentEnabledChatTypesPreset(preset.Id);
                }
            }
        }

        private unsafe void PollTalkAddon(Framework framework)
        {
            if (!this.config.Enabled) return;
            if (!this.config.ReadFromQuestTalkAddon) return;
            if (!ClientState.IsLoggedIn)
            {
                this.talkAddonPtr = IntPtr.Zero;
                return;
            }

            if (this.talkAddonPtr == IntPtr.Zero)
            {
                this.talkAddonPtr = Gui.GetAddonByName("Talk", 1);
                return;
            }

            var talkAddon = (AddonTalk*)this.talkAddonPtr.ToPointer();
            if (talkAddon == null) return;

            // Clear the last text if the window isn't visible.
            if (!TalkUtils.IsVisible(talkAddon))
            {
                // Cancel TTS when the dialogue window is closed, if configured
                if (this.config.CancelSpeechOnTextAdvance)
                {
                    this.backendManager.CancelSay(TextSource.TalkAddon);
                }

                SetLastQuestText("");
                return;
            }

            TalkAddonText talkAddonText;
            try
            {
                talkAddonText = TalkUtils.ReadTalkAddon(Data, talkAddon);
            }
            catch (NullReferenceException)
            {
                // Just swallow the NRE, I have no clue what causes this but it only happens when relogging in rare cases
                return;
            }

            var text = talkAddonText.Text;

            if (text == "" || IsDuplicateQuestText(text)) return;
            SetLastQuestText(text);

            if (talkAddonText.Speaker != "" && ShouldSaySender())
            {
                if (!this.config.DisallowMultipleSay || !IsSameSpeaker(talkAddonText.Speaker))
                {
                    text = $"{talkAddonText.Speaker} says {text}";
                    SetLastSpeaker(talkAddonText.Speaker);
                }
            }

            var speaker = Objects.FirstOrDefault(gObj => gObj.Name.TextValue == talkAddonText.Speaker);

            // Cancel TTS if it's currently Talk addon text, if configured
            if (this.config.CancelSpeechOnTextAdvance && this.backendManager.GetCurrentlySpokenTextSource() == TextSource.TalkAddon)
            {
                this.backendManager.CancelSay(TextSource.TalkAddon);
            }

            Say(speaker, text, TextSource.TalkAddon);
        }

        private bool notifiedFailedToBindPort;
        private void CheckFailedToBindPort(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            if (!ClientState.IsLoggedIn || !this.sharedState.WSFailedToBindPort || this.notifiedFailedToBindPort) return;
            Chat.Print($"TextToTalk failed to bind to port {config.WebsocketPort}. " +
                       "Please close the owner of that port and reload the Websocket server, " +
                       "or select a different port.");
            this.notifiedFailedToBindPort = true;
        }

        private unsafe void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            if (!this.config.Enabled) return;

            var textValue = message.TextValue;
            if (IsDuplicateQuestText(textValue)) return;

#if DEBUG
            PluginLog.Log("Chat message from type {0}: {1}", type, textValue);
#endif

            // This section controls speaker-related functions.
            if (sender != null && sender.TextValue != string.Empty)
            {
                if (ShouldSaySender(type))
                {
                    // If we allow the speaker's name to be repeated each time the speak,
                    // or the speaker has actually changed.
                    if (!this.config.DisallowMultipleSay || !IsSameSpeaker(sender.TextValue))
                    {
                        if ((int)type == (int)AdditionalChatType.NPCDialogue)
                        {
                            // (TextToTalk#40) If we're reading from the Talk addon when NPC dialogue shows up, just return from this.
                            var talkAddon = (AddonTalk*)this.talkAddonPtr.ToPointer();
                            if (this.config.ReadFromQuestTalkAddon && talkAddon != null && TalkUtils.IsVisible(talkAddon))
                            {
                                return;
                            }

                            SetLastQuestText(textValue);
                        }

                        textValue = $"{sender.TextValue} says {textValue}";
                        SetLastSpeaker(sender.TextValue);
                    }
                }
            }

            if (this.config.Bad.Where(t => t.Text != "").Any(t => t.Match(textValue))) return;

            var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();

            var typeAccepted = chatTypes.EnabledChatTypes.Contains((int)type);
            var goodMatch = this.config.Good
                .Where(t => t.Text != "")
                .Any(t => t.Match(textValue));
            if (!(chatTypes.EnableAllChatTypes || typeAccepted) || this.config.Good.Count > 0 && !goodMatch) return;

            var senderText = sender?.TextValue; // Can't access in lambda
            var speaker = Objects.FirstOrDefault(a => a.Name.TextValue == senderText);

            Say(speaker, textValue, TextSource.Chat);
        }

        private void Say(GameObject speaker, string textValue, TextSource source)
        {
            if ((ObjectKind)speaker.SubKind == ObjectKind.Player && this.config.UsePlayerRateLimiter && !this.rateLimiter.Check(speaker.Name.TextValue))
            {
                return;
            }

            var cleanText = Pipe(
                textValue,
                TalkUtils.StripAngleBracketedText,
                TalkUtils.ReplaceSsmlTokens,
                TalkUtils.NormalizePunctuation,
                TalkUtils.RemoveStutters)
                .Trim();

            if (!cleanText.Any() || !TalkUtils.IsSpeakable(cleanText))
            {
                return;
            }

            var gender = this.config.UseGenderedVoicePresets ? GetCharacterGender(speaker) : Gender.None;
            this.backendManager.Say(source, gender, cleanText);
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
            if (this.ungenderedOverrides.IsUngendered(modelId))
            {
                actorGender = Gender.None;
                PluginLog.Log($"Got model ID {modelId} for {gObj.ObjectKind} \"{gObj.Name}\" (gender overriden to: {actorGender})");
            }
            else
            {
                PluginLog.Log($"Got model ID {modelId} for {gObj.ObjectKind} \"{gObj.Name}\" (gender read as: {actorGender})");
            }

            return actorGender;
        }

        private void OpenConfigUi()
        {
            this.ui.ShowWindow<ConfigurationWindow>();
        }

        private bool IsDuplicateQuestText(string text)
        {
            return this.sharedState.LastQuestText == text;
        }

        private void SetLastQuestText(string text)
        {
            this.sharedState.LastQuestText = text;
        }

        private bool IsSameSpeaker(string speaker)
        {
            return this.sharedState.LastSpeaker == speaker;
        }

        private void SetLastSpeaker(string speaker)
        {
            this.sharedState.LastSpeaker = speaker;
        }

        private bool ShouldSaySender()
        {
            return this.config.EnableNameWithSay && this.config.NameNpcWithSay;
        }

        private bool ShouldSaySender(XivChatType type)
        {
            return this.config.EnableNameWithSay && (this.config.NameNpcWithSay || (int)type != (int)AdditionalChatType.NPCDialogue);
        }

        private static T Pipe<T>(T input, params Func<T, T>[] transforms)
        {
            return transforms.Aggregate(input, (agg, next) => next(agg));
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            Framework.Update -= PollTalkAddon;
            Framework.Update -= CheckKeybindPressed;
            Framework.Update -= CheckPresetKeybindPressed;

            Chat.ChatMessage -= CheckFailedToBindPort;
            Chat.ChatMessage -= OnChatMessage;

            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            PluginInterface.UiBuilder.Draw -= this.ui.Draw;

            PluginInterface.SavePluginConfig(this.config);

            this.serviceCollection.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
