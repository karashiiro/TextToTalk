using Dalamud.CrystalTower.Commands;
using Dalamud.CrystalTower.UI;
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
using System.Runtime.InteropServices;
using TextToTalk.Backends;
using TextToTalk.GameEnums;
using TextToTalk.Middleware;
using TextToTalk.Modules;
using TextToTalk.Talk;
using TextToTalk.UI.Dalamud;
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

        private readonly PluginConfiguration config;
        private readonly CommandManager commandManager;
        private readonly MainCommandModule commandModule;
        private readonly Services services;
        private readonly KeyState keys;

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

            var talkAddonHandler = this.services.GetService<TalkAddonHandler>();
            talkAddonHandler.Say += Say;

            var chatMessageHandler = this.services.GetService<ChatMessageHandler>();
            chatMessageHandler.Say += Say;

            pi.UiBuilder.Draw += ui.Draw;
            pi.UiBuilder.OpenConfigUi += OpenConfigUi;

            var keyState = this.services.GetService<KeyState>();
            this.keys = keyState;

            var chat = this.services.GetService<ChatGui>();
            chat.ChatMessage += OnChatMessage;
            chat.ChatMessage += CheckFailedToBindPort;

            var framework = this.services.GetService<Framework>();
            framework.Update += PollTalkAddon;
            framework.Update += CheckKeybindPressed;

            var commands = this.services.GetService<Dalamud.Game.Command.CommandManager>();
            this.commandManager = new CommandManager(commands, this.services);
            this.commandManager.AddCommandModule<MainCommandModule>();

            this.commandModule = this.commandManager.GetCommandModule<MainCommandModule>();
        }

        private bool keysDown = false;
        private void CheckKeybindPressed(Framework framework)
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
                    this.commandModule.Chat.Print($"TextToTalk preset -> {preset.Name}");
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

        private void PollTalkAddon(Framework framework)
        {
            if (!this.config.Enabled) return;
            if (!this.config.ReadFromQuestTalkAddon) return;
            var talkAddonHandler = this.services.GetService<TalkAddonHandler>();
            talkAddonHandler.PollAddon(framework);
        }

        private bool notifiedFailedToBindPort;
        private void CheckFailedToBindPort(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            var sharedState = this.services.GetService<SharedState>();
            var clientState = this.services.GetService<ClientState>();
            var chat = this.services.GetService<ChatGui>();

            if (!clientState.IsLoggedIn || !sharedState.WSFailedToBindPort || this.notifiedFailedToBindPort) return;
            chat.Print($"TextToTalk failed to bind to port {config.WebsocketPort}. " +
                       "Please close the owner of that port and reload the Websocket server, " +
                       "or select a different port.");
            this.notifiedFailedToBindPort = true;
        }

        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            if (!this.config.Enabled) return;
            var chatMessageHandler = this.services.GetService<ChatMessageHandler>();
            chatMessageHandler.ProcessMessage(type, id, ref sender, ref message, ref handled);
        }

        private void Say(GameObject speaker, string textValue, TextSource source)
        {
            if (ShouldRateLimit(speaker))
            {
                return;
            }

            string cleanText;

            if (config.RemoveStutterEnabled)
            {
                cleanText = Pipe(
                textValue,
                TalkUtils.StripAngleBracketedText,
                TalkUtils.ReplaceSsmlTokens,
                TalkUtils.NormalizePunctuation,
                TalkUtils.RemoveStutters,
                x => x.Trim());
            }
            else
            {
                cleanText = Pipe(
                textValue,
                TalkUtils.StripAngleBracketedText,
                TalkUtils.ReplaceSsmlTokens,
                TalkUtils.NormalizePunctuation,
                x => x.Trim());
            }

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

        private static T Pipe<T>(T input, params Func<T, T>[] transforms)
        {
            return transforms.Aggregate(input, (agg, next) => next(agg));
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            var framework = this.services.GetService<Framework>();
            framework.Update -= PollTalkAddon;
            framework.Update -= CheckKeybindPressed;

            var chat = this.services.GetService<ChatGui>();
            chat.ChatMessage -= CheckFailedToBindPort;
            chat.ChatMessage -= OnChatMessage;

            var ui = this.services.GetService<WindowManager>();
            var pi = this.services.GetService<DalamudPluginInterface>();
            pi.UiBuilder.OpenConfigUi -= OpenConfigUi;
            pi.UiBuilder.Draw -= ui.Draw;

            pi.SavePluginConfig(this.config);

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
