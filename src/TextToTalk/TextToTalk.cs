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
using System.Net.Http;
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

        private readonly PluginConfiguration config;
        private readonly CommandManager commandManager;
        private readonly Services services;

        private IntPtr talkAddonPtr;

        public string Name => "TextToTalk";

        public TextToTalk([RequiredVersion("1.0")] DalamudPluginInterface pi)
        {
            this.config = (PluginConfiguration)pi.GetPluginConfig() ?? new PluginConfiguration();
            this.config.Initialize(pi);

            this.services = Services.Create(pi, this.config);

            var ui = this.services.GetService<WindowManager>();
            
            ui.AddWindow<UnlockerResultWindow>(initiallyVisible: false);
            ui.AddWindow<VoiceUnlockerWindow>(initiallyVisible: false);
            ui.AddWindow<ChannelPresetModificationWindow>(initiallyVisible: false);
            ui.AddWindow<ConfigurationWindow>(InitiallyVisible);

            this.services.PluginInterface.UiBuilder.Draw += ui.Draw;
            this.services.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            this.services.Chat.ChatMessage += OnChatMessage;
            this.services.Chat.ChatMessage += CheckFailedToBindPort;

            this.services.Framework.Update += PollTalkAddon;
            this.services.Framework.Update += CheckKeybindPressed;
            this.services.Framework.Update += CheckPresetKeybindPressed;

            this.commandManager = new CommandManager(this.services.Commands, this.services);
            this.commandManager.AddCommandModule<MainCommandModule>();
        }

        private bool keysDown;
        private void CheckKeybindPressed(Framework framework)
        {
            if (!this.config.UseKeybind) return;

            var keys = this.services.Keys;
            if (keys[(byte)this.config.ModifierKey] &&
                keys[(byte)this.config.MajorKey])
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
                var keys = this.services.Keys;
                if (keys[(byte)preset.ModifierKey] &&
                    keys[(byte)preset.MajorKey])
                {
                    this.config.SetCurrentEnabledChatTypesPreset(preset.Id);
                }
            }
        }

        private unsafe void PollTalkAddon(Framework framework)
        {
            if (!this.config.Enabled) return;
            if (!this.config.ReadFromQuestTalkAddon) return;
            if (!this.services.ClientState.IsLoggedIn)
            {
                this.talkAddonPtr = IntPtr.Zero;
                return;
            }

            if (this.talkAddonPtr == IntPtr.Zero)
            {
                this.talkAddonPtr = this.services.Gui.GetAddonByName("Talk", 1);
                return;
            }

            var talkAddon = (AddonTalk*)this.talkAddonPtr.ToPointer();
            if (talkAddon == null) return;

            var backendManager = this.services.GetService<VoiceBackendManager>();

            // Clear the last text if the window isn't visible.
            if (!TalkUtils.IsVisible(talkAddon))
            {
                // Cancel TTS when the dialogue window is closed, if configured
                if (this.config.CancelSpeechOnTextAdvance)
                {
                    backendManager.CancelSay(TextSource.TalkAddon);
                }

                SetLastQuestText("");
                return;
            }

            TalkAddonText talkAddonText;
            try
            {
                talkAddonText = TalkUtils.ReadTalkAddon(this.services.Data, talkAddon);
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

            var speaker = this.services.Objects.FirstOrDefault(gObj => gObj.Name.TextValue == talkAddonText.Speaker);

            // Cancel TTS if it's currently Talk addon text, if configured
            if (this.config.CancelSpeechOnTextAdvance && backendManager.GetCurrentlySpokenTextSource() == TextSource.TalkAddon)
            {
                backendManager.CancelSay(TextSource.TalkAddon);
            }

            Say(speaker, text, TextSource.TalkAddon);
        }

        private bool notifiedFailedToBindPort;
        private void CheckFailedToBindPort(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            var sharedState = this.services.GetService<SharedState>();

            if (!this.services.ClientState.IsLoggedIn || !sharedState.WSFailedToBindPort || this.notifiedFailedToBindPort) return;
            this.services.Chat.Print($"TextToTalk failed to bind to port {config.WebsocketPort}. " +
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
            var speaker = this.services.Objects.FirstOrDefault(a => a.Name.TextValue == senderText);

            Say(speaker, textValue, TextSource.Chat);
        }

        private void Say(GameObject speaker, string textValue, TextSource source)
        {
            if (ShouldRateLimit(speaker))
            {
                return;
            }

            var cleanText = Pipe(
                textValue,
                TalkUtils.StripAngleBracketedText,
                TalkUtils.ReplaceSsmlTokens,
                TalkUtils.NormalizePunctuation,
                TalkUtils.RemoveStutters,
                x => x.Trim());

            if (!cleanText.Any() || !TalkUtils.IsSpeakable(cleanText))
            {
                return;
            }

            var gender = this.config.UseGenderedVoicePresets ? GetCharacterGender(speaker) : Gender.None;
            var backendManager = this.services.GetService<VoiceBackendManager>();
            backendManager.Say(source, gender, cleanText);
        }

        private bool ShouldRateLimit(GameObject speaker)
        {
            var rateLimiter = this.services.GetService<RateLimiter>();
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
            var ungenderedOverrides = this.services.GetService<UngenderedOverrideManager>();
            if (ungenderedOverrides.IsUngendered(modelId))
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
            var ui = this.services.GetService<WindowManager>();
            ui.ShowWindow<ConfigurationWindow>();
        }

        private bool IsDuplicateQuestText(string text)
        {
            var sharedState = this.services.GetService<SharedState>();
            return sharedState.LastQuestText == text;
        }

        private void SetLastQuestText(string text)
        {
            var sharedState = this.services.GetService<SharedState>();
            sharedState.LastQuestText = text;
        }

        private bool IsSameSpeaker(string speaker)
        {
            var sharedState = this.services.GetService<SharedState>();
            return sharedState.LastSpeaker == speaker;
        }

        private void SetLastSpeaker(string speaker)
        {
            var sharedState = this.services.GetService<SharedState>();
            sharedState.LastSpeaker = speaker;
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

            this.services.Framework.Update -= PollTalkAddon;
            this.services.Framework.Update -= CheckKeybindPressed;
            this.services.Framework.Update -= CheckPresetKeybindPressed;

            this.services.Chat.ChatMessage -= CheckFailedToBindPort;
            this.services.Chat.ChatMessage -= OnChatMessage;

            var ui = this.services.GetService<WindowManager>();
            this.services.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            this.services.PluginInterface.UiBuilder.Draw -= ui.Draw;

            this.services.PluginInterface.SavePluginConfig(this.config);

            this.services.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
