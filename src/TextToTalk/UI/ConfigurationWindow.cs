using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Subjects;
using System.Text;
using Dalamud.Data;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using TextToTalk.Backends;
using TextToTalk.GameEnums;

namespace TextToTalk.UI
{
    public class ConfigurationWindow : Window, IDisposable
    {
        private static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
        private static readonly Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
        private static readonly Vector4 Red = new(1.0f, 0.0f, 0.0f, 1.0f);

        private readonly PluginConfiguration config;
        private readonly DataManager data;
        private readonly VoiceBackendManager backendManager;
        private readonly PlayerService players;
        private readonly NpcService npc;
        private readonly IConfigUIDelegates helpers;
        private readonly Subject<bool> onPresetOpenRequested;

        private IDictionary<Guid, string> playerWorldEditing = new Dictionary<Guid, string>();
        private IDictionary<Guid, bool> playerWorldValid = new Dictionary<Guid, bool>();
        private string playerName = string.Empty;
        private string playerWorld = string.Empty;
        private string playerWorldError = string.Empty;
        private string? npcName = string.Empty;
        private string npcError = string.Empty;

        public ConfigurationWindow(PluginConfiguration config, DataManager data, VoiceBackendManager backendManager,
            PlayerService players, NpcService npc, Window voiceUnlockerWindow) : base(
            "TextToTalk Configuration###TextToTalkConfig")
        {
            this.config = config;
            this.data = data;
            this.backendManager = backendManager;
            this.players = players;
            this.npc = npc;
            this.helpers = new ConfigUIDelegates { OpenVoiceUnlockerAction = () => voiceUnlockerWindow.IsOpen = true };
            this.onPresetOpenRequested = new Subject<bool>();

            Size = new Vector2(540, 480);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public IObservable<bool> OnPresetOpenRequested()
        {
            return this.onPresetOpenRequested;
        }

        public override void PreDraw()
        {
            WindowName =
                $"TextToTalk Configuration (TTS {(this.config.Enabled ? "Enabled" : "Disabled")})###TextToTalkConfig";

            var titleBarColor = this.backendManager.GetBackendTitleBarColor();
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, titleBarColor != default
                ? titleBarColor
                : ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TitleBgActive)));
        }

        public override void PostDraw()
        {
            ImGui.PopStyleColor();
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar($"TextToTalk##{MemoizedId.Create()}"))
            {
                if (ImGui.BeginTabItem("Synthesizer Settings"))
                {
                    DrawSynthesizerSettings();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Player Voices"))
                {
                    DrawPlayerVoiceSettings();
                    ImGui.EndTabItem();
                }
                else if (this.playerWorldEditing.Count > 0)
                {
                    // Clear all user edits if the tab isn't selected anymore
                    this.playerWorldEditing = new Dictionary<Guid, string>();
                    this.playerWorldValid = new Dictionary<Guid, bool>();
                    this.playerName = string.Empty;
                    this.playerWorld = string.Empty;
                    this.playerWorldError = string.Empty;
                }

                if (ImGui.BeginTabItem("NPC Voices"))
                {
                    DrawNpcVoiceSettings();
                    ImGui.EndTabItem();
                }
                else
                {
                    // Clear all user edits if the tab isn't selected anymore
                    this.npcName = string.Empty;
                    this.npcError = string.Empty;
                }

                if (ImGui.BeginTabItem("Channel Settings"))
                {
                    DrawChannelSettings();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Triggers/Exclusions"))
                {
                    DrawTriggersExclusions();
                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        private void DrawSynthesizerSettings()
        {
            if (ImGui.CollapsingHeader($"Keybinds##{MemoizedId.Create()}"))
            {
                ConfigComponents.ToggleUseKeybind($"Enable Keybind##{MemoizedId.Create()}", this.config);

                ImGui.PushItemWidth(100f);
                var kItem1 = VirtualKey.EnumToIndex(this.config.ModifierKey);
                if (ImGui.Combo($"##{MemoizedId.Create()}", ref kItem1, VirtualKey.Names.Take(3).ToArray(), 3))
                {
                    this.config.ModifierKey = VirtualKey.IndexToEnum(kItem1);
                    this.config.Save();
                }

                ImGui.SameLine();
                var kItem2 = VirtualKey.EnumToIndex(this.config.MajorKey) - 3;
                if (ImGui.Combo($"TTS Toggle Keybind##{MemoizedId.Create()}", ref kItem2,
                        VirtualKey.Names.Skip(3).ToArray(), VirtualKey.Names.Length - 3))
                {
                    this.config.MajorKey = VirtualKey.IndexToEnum(kItem2 + 3);
                    this.config.Save();
                }

                ImGui.PopItemWidth();
            }

            if (ImGui.CollapsingHeader("General"))
            {
                ConfigComponents.ToggleReadFromQuestTalkAddon(
                    "Read NPC dialogue from the dialogue window",
                    this.config);

                if (this.config.ReadFromQuestTalkAddon)
                {
                    ImGui.Spacing();
                    ImGui.Indent();

                    ConfigComponents.ToggleCancelSpeechOnTextAdvance(
                        "Cancel the current NPC speech when new text is available or text is advanced",
                        this.config);
                    ConfigComponents.ToggleSkipVoicedQuestText(
                        "Skip reading voice-acted NPC dialogue",
                        this.config);

                    ImGui.Unindent();
                }

                ImGui.Spacing();
                ConfigComponents.ToggleReadFromBattleTalkAddon(
                    "Read NPC dialogue from the battle dialogue window",
                    this.config);

                if (this.config.ReadFromBattleTalkAddon)
                {
                    ImGui.Spacing();
                    ImGui.Indent();

                    ConfigComponents.ToggleSkipVoicedBattleText(
                        "Skip reading voice-acted NPC dialogue",
                        this.config);

                    ImGui.Unindent();
                }

                ImGui.Spacing();
                ConfigComponents.ToggleSkipMessagesFromYou("Skip messages from you", this.config);

                ImGui.Spacing();
                ConfigComponents.ToggleEnableNameWithSay("Enable \"X says:\" when people speak", this.config);

                if (this.config.EnableNameWithSay)
                {
                    ImGui.Spacing();
                    ImGui.Indent();

                    ConfigComponents.ToggleNameNpcWithSay("Also say \"NPC Name says:\" in NPC dialogue", this.config);
                    ConfigComponents.ToggleSayPlayerWorldName("Say player world name", this.config);
                    ConfigComponents.ToggleDisallowMultipleSay(
                        "Only say \"Character Name says:\" the first time a character speaks",
                        this.config);
                    ConfigComponents.ToggleSayPartialName("Only say forename or surname", this.config);

                    if (this.config.SayPartialName)
                    {
                        ImGui.Spacing();
                        ImGui.Indent();

                        var onlySayFirstOrLastName = (int)this.config.OnlySayFirstOrLastName;

                        if (ImGui.RadioButton("Only say forename", ref onlySayFirstOrLastName,
                                (int)FirstOrLastName.First))
                        {
                            this.config.OnlySayFirstOrLastName = FirstOrLastName.First;
                            this.config.Save();
                        }

                        if (ImGui.RadioButton("Only say surname", ref onlySayFirstOrLastName,
                                (int)FirstOrLastName.Last))
                        {
                            this.config.OnlySayFirstOrLastName = FirstOrLastName.Last;
                            this.config.Save();
                        }

                        ImGui.Unindent();
                    }

                    ImGui.Unindent();
                }

                ConfigComponents.ToggleUsePlayerRateLimiter("Limit player TTS frequency", this.config);

                var messagesPerSecond = this.config.MessagesPerSecond;
                if (this.config.UsePlayerRateLimiter)
                {
                    ImGui.Indent();

                    if (ImGui.DragFloat("", ref messagesPerSecond, 0.1f, 0.1f, 30, "%.3f message(s)/s"))
                    {
                        this.config.MessagesPerSecond = messagesPerSecond;
                    }

                    ImGui.Unindent();
                }
            }

            if (ImGui.CollapsingHeader($"Voices##{MemoizedId.Create()}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var backends = Enum.GetValues<TTSBackend>();
                var backendsDisplay = backends.Select(b => b.GetFormattedName()).ToArray();
                var backend = this.config.Backend;
                var backendIndex = Array.IndexOf(backends, backend);

                if (ImGui.Combo($"Voice backend##{MemoizedId.Create()}", ref backendIndex, backendsDisplay,
                        backends.Length))
                {
                    var newBackend = backends[backendIndex];

                    this.config.Backend = newBackend;
                    this.config.Save();

                    this.backendManager.SetBackend(newBackend);
                }

                if (!this.backendManager.BackendLoading)
                {
                    // Draw the settings for the specific backend we're using.
                    this.backendManager.DrawSettings(this.helpers);
                }
            }

            if (ImGui.CollapsingHeader("Experimental", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ConfigComponents.ToggleRemoveStutterEnabled(
                    "Attempt to remove stutter from NPC dialogue (default: On)",
                    this.config);
            }
        }

        private void DrawPlayerVoiceSettings()
        {
            ImGui.TextColored(HintColor, "Set specific voice presets for players using the options below.");

            ImGui.Spacing();

            var tableSize = new Vector2(0.0f, 300f);
            var presets = this.config.GetVoicePresetsForBackend(this.config.Backend).ToList();
            presets.Sort((a, b) => a.Id - b.Id);
            var presetArray = presets.Select(p => p.Name).ToArray();
            var toDelete = new List<PlayerInfo>();
            Components.Table($"##{MemoizedId.Create()}", tableSize, ImGuiTableFlags.Borders,
                () =>
                {
                    ImGui.TableSetupScrollFreeze(0, 1); // Make top row always visible
                    ImGui.TableSetupColumn($"##{MemoizedId.Create()}", ImGuiTableColumnFlags.None, 30f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 280f);
                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.None, 100f);
                    ImGui.TableSetupColumn("Preset", ImGuiTableColumnFlags.None, 220f);
                    ImGui.TableHeadersRow();
                },
                () => this.config.Players
                    .Select(row =>
                    {
                        var (id, playerInfo) = row;

                        if (!this.playerWorldEditing.TryGetValue(id, out var worldName))
                        {
                            var world = data.GetExcelSheet<World>()?.GetRow(playerInfo.WorldId);
                            this.playerWorldEditing[id] = world?.Name.ToString() ?? "";
                        }

                        return (id, playerInfo, worldName);
                    }),
                row =>
                {
                    var (id, playerInfo, _) = row;

                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(
                            $"{FontAwesomeIcon.Trash.ToIconString()}##{MemoizedId.Create(uniq: id.ToString())}"))
                    {
                        toDelete.Add(playerInfo);
                    }

                    ImGui.PopFont();

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Delete");
                        ImGui.EndTooltip();
                    }
                },
                row =>
                {
                    var (id, playerInfo, worldName) = row;
                    var name = playerInfo.Name;

                    // Allow player names to be edited in the table
                    if (ImGui.InputText($"##{MemoizedId.Create(uniq: id.ToString())}", ref name, 32))
                    {
                        playerInfo.Name = name;
                        this.config.Save();
                        DetailedLog.Debug($"Updated player name: {playerInfo.Name}@{worldName ?? ""}");
                    }
                },
                row =>
                {
                    var (id, playerInfo, worldName) = row;

                    // Allow player worlds to be edited in the table
                    worldName ??= "";
                    if (ImGui.InputText($"##{MemoizedId.Create(uniq: id.ToString())}", ref worldName, 32))
                    {
                        this.playerWorldEditing[id] = worldName;

                        // Try to get the input world
                        var worldPending = GetWorldForUserInput(worldName);

                        // Only save the result if the name actually matches a world
                        if (worldPending != null)
                        {
                            this.playerWorldValid[id] = true;
                            playerInfo.WorldId = worldPending.RowId;
                            this.config.Save();
                            DetailedLog.Debug($"Updated player world: {playerInfo.Name}@{worldPending.Name}");
                        }
                        else
                        {
                            this.playerWorldValid[id] = false;
                        }
                    }

                    // Indicate if the operation succeeded
                    if (this.playerWorldValid.TryGetValue(id, out var valid))
                    {
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (valid)
                        {
                            ImGui.TextColored(Green, FontAwesomeIcon.CheckCircle.ToIconString());
                        }
                        else
                        {
                            ImGui.TextColored(Red, FontAwesomeIcon.MinusCircle.ToIconString());
                        }

                        ImGui.PopFont();
                    }
                },
                row =>
                {
                    var (id, playerInfo, worldName) = row;
                    var name = playerInfo.Name;

                    // Player voice dropdown
                    var presetIndex = this.players.TryGetPlayerVoice(playerInfo, out var v) ? presets.IndexOf(v) : 0;
                    if (ImGui.Combo($"##{MemoizedId.Create(uniq: id.ToString())}", ref presetIndex, presetArray,
                            presets.Count))
                    {
                        this.players.SetPlayerVoice(playerInfo, presets[presetIndex]);
                        this.config.Save();
                        DetailedLog.Debug($"Updated voice for {name}@{worldName}: {presets[presetIndex].Name}");
                    }
                });

            if (toDelete.Any())
            {
                foreach (var playerInfo in toDelete)
                {
                    this.players.DeletePlayer(playerInfo);
                }

                this.config.Save();
            }

            ImGui.InputText($"Player name##{MemoizedId.Create()}", ref this.playerName, 32);
            ImGui.InputText($"Player world##{MemoizedId.Create()}", ref this.playerWorld, 32);
            if (!string.IsNullOrEmpty(this.playerWorldError))
            {
                ImGui.TextColored(Red, this.playerWorldError);
            }

            if (ImGui.Button($"Add player##{MemoizedId.Create()}"))
            {
                // Validate data before saving the new player
                var world = GetWorldForUserInput(this.playerWorld);
                if (world != null && this.players.AddPlayer(this.playerName, world.RowId))
                {
                    this.config.Save();
                    DetailedLog.Info($"Added player: {this.playerName}@{world.Name}");
                }
                else if (world == null)
                {
                    this.playerWorldError = "Unknown world.";
                    DetailedLog.Error("The provided world name was invalid");
                }
                else
                {
                    this.playerWorldError = "Failed to add player - is this a duplicate?";
                    DetailedLog.Error("Failed to add player; this might be a duplicate entry");
                }
            }
        }

        private World? GetWorldForUserInput(string worldName)
        {
            return data.GetExcelSheet<World>()?
                .Where(w => w.IsPublic)
                .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                .FirstOrDefault(w =>
                    string.Equals(w.Name.RawString, worldName, StringComparison.InvariantCultureIgnoreCase));
        }

        private void DrawNpcVoiceSettings()
        {
            ImGui.TextColored(HintColor, "Set specific voice presets for NPCs using the options below.");

            ImGui.Spacing();

            var tableSize = new Vector2(0.0f, 300f);
            var presets = this.config.GetVoicePresetsForBackend(this.config.Backend).ToList();
            presets.Sort((a, b) => a.Id - b.Id);
            var presetArray = presets.Select(p => p.Name).ToArray();
            var toDelete = new List<NpcInfo>();
            Components.Table($"##{MemoizedId.Create()}", tableSize, ImGuiTableFlags.Borders,
                () =>
                {
                    ImGui.TableSetupScrollFreeze(0, 1); // Make top row always visible
                    ImGui.TableSetupColumn($"##{MemoizedId.Create()}", ImGuiTableColumnFlags.None, 30f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 300f);
                    ImGui.TableSetupColumn("Preset", ImGuiTableColumnFlags.None, 300f);
                    ImGui.TableHeadersRow();
                },
                () => this.config.Npcs,
                row =>
                {
                    var (id, npcInfo) = row;

                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(
                            $"{FontAwesomeIcon.Trash.ToIconString()}##{MemoizedId.Create(uniq: id.ToString())}"))
                    {
                        toDelete.Add(npcInfo);
                    }

                    ImGui.PopFont();

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Delete");
                        ImGui.EndTooltip();
                    }
                },
                row =>
                {
                    var (id, npcInfo) = row;
                    var name = npcInfo.Name;

                    // Allow NPC names to be edited in the table
                    if (ImGui.InputText($"##{MemoizedId.Create(uniq: id.ToString())}", ref name, 32))
                    {
                        npcInfo.Name = name;
                        this.config.Save();
                        DetailedLog.Debug($"Updated NPC name: {npcInfo.Name}");
                    }
                },
                row =>
                {
                    var (id, npcInfo) = row;
                    var name = npcInfo.Name;

                    // NPC voice dropdown
                    var presetIndex = this.npc.TryGetNpcVoice(npcInfo, out var v) ? presets.IndexOf(v) : 0;
                    if (ImGui.Combo($"##{MemoizedId.Create(uniq: id.ToString())}", ref presetIndex, presetArray,
                            presets.Count))
                    {
                        this.npc.SetNpcVoice(npcInfo, presets[presetIndex]);
                        this.config.Save();
                        DetailedLog.Debug($"Updated voice for {name}: {presets[presetIndex].Name}");
                    }
                });

            if (toDelete.Any())
            {
                foreach (var npcInfo in toDelete)
                {
                    this.npc.DeleteNpc(npcInfo);
                }

                this.config.Save();
            }

            ImGui.InputText($"NPC name##{MemoizedId.Create()}", ref this.npcName, 32);

            if (!string.IsNullOrEmpty(this.npcError))
            {
                ImGui.TextColored(Red, this.npcError);
            }

            if (ImGui.Button($"Add NPC##{MemoizedId.Create()}"))
            {
                if (this.npc.AddNpc(this.npcName))
                {
                    this.config.Save();
                    DetailedLog.Info($"Added NPC: {this.npcName}");
                }
                else
                {
                    this.npcError = "Failed to add NPC - is this a duplicate?";
                    DetailedLog.Error("Failed to add NPC; this might be a duplicate entry");
                }
            }
        }

        private void DrawChannelSettings()
        {
            var currentEnabledChatTypesPreset = this.config.GetCurrentEnabledChatTypesPreset();

            var presets = this.config.EnabledChatTypesPresets.ToList();
            presets.Sort((a, b) => a.Id - b.Id);
            var presetIndex = presets.IndexOf(currentEnabledChatTypesPreset);
            if (ImGui.Combo($"Preset##{MemoizedId.Create()}", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                    presets.Count))
            {
                this.config.CurrentPresetId = presets[presetIndex].Id;
                this.config.Save();
            }

            if (ImGui.Button($"New preset##{MemoizedId.Create()}"))
            {
                var newPreset = this.config.NewChatTypesPreset();
                this.config.SetCurrentEnabledChatTypesPreset(newPreset.Id);
                this.onPresetOpenRequested.OnNext(true);
            }

            ImGui.SameLine();

            if (ImGui.Button($"Edit##{MemoizedId.Create()}"))
            {
                this.onPresetOpenRequested.OnNext(true);
            }

            if (this.config.EnabledChatTypesPresets.Count > 1)
            {
                ImGui.SameLine();
                if (ImGui.Button($"Delete##{MemoizedId.Create()}"))
                {
                    var otherPreset =
                        this.config.EnabledChatTypesPresets.First(p => p.Id != currentEnabledChatTypesPreset.Id);
                    this.config.SetCurrentEnabledChatTypesPreset(otherPreset.Id);
                    this.config.EnabledChatTypesPresets.Remove(currentEnabledChatTypesPreset);
                }
            }

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), "Recommended for trigger use");
            EnabledChatTypesPresetComponents.ToggleEnableAllChatTypes(
                "Enable all (including undocumented)",
                currentEnabledChatTypesPreset);

            if (currentEnabledChatTypesPreset.EnableAllChatTypes) return;
            ImGui.Spacing();

            var channels = Enum.GetNames(typeof(XivChatType)).Concat(Enum.GetNames(typeof(AdditionalChatType)));
            foreach (var channel in channels)
            {
                XivChatType enumValue;
                try
                {
                    enumValue = (XivChatType)Enum.Parse(typeof(XivChatType), channel);
                }
                catch (ArgumentException)
                {
                    enumValue = (XivChatType)(int)Enum.Parse(typeof(AdditionalChatType), channel);
                }

                var selected = currentEnabledChatTypesPreset.EnabledChatTypes?.Contains((int)enumValue) ?? false;
                if (!ImGui.Checkbox(channel == "PvPTeam" ? "PvP Team" : SplitWords(channel), ref selected)) continue;
                var isEnabled = currentEnabledChatTypesPreset.EnabledChatTypes?.Contains((int)enumValue) ?? false;
                if (isEnabled)
                {
                    currentEnabledChatTypesPreset.EnabledChatTypes?.Remove((int)enumValue);
                    this.config.Save();
                }
                else
                {
                    currentEnabledChatTypesPreset.EnabledChatTypes?.Add((int)enumValue);
                    this.config.Save();
                }
            }
        }

        private static string SplitWords(string oneWord)
        {
            var words = oneWord
                .Select(c => c)
                .Skip(1)
                .Aggregate("" + oneWord[0],
                    (acc, c) => acc + (c is >= 'A' and <= 'Z' or >= '0' and <= '9' ? " " + c : "" + c))
                .Split(' ');

            var finalWords = new StringBuilder(oneWord.Length + 3);
            for (var i = 0; i < words.Length - 1; i++)
            {
                finalWords.Append(words[i]);
                if (words[i].Length == 1 && words[i + 1].Length == 1)
                {
                    continue;
                }

                finalWords.Append(" ");
            }

            return finalWords.Append(words.Last()).ToString();
        }

        private void DrawTriggersExclusions()
        {
            var currentConfiguration = this.config.GetCurrentEnabledChatTypesPreset();
            EnabledChatTypesPresetComponents.ToggleEnableAllChatTypes(
                "Enable all chat types (including undocumented)",
                currentConfiguration);

            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), "Recommended for trigger use");
            ImGui.Dummy(new Vector2(0, 5));

            ExpandyList("Trigger", this.config.Good);
            ExpandyList("Exclusion", this.config.Bad);
        }

        private void ExpandyList(string kind, IList<Trigger> listItems)
        {
            ImGui.Text($"{kind}s");

            for (var i = 0; i < listItems.Count; i++)
            {
                var str = listItems[i].Text;
                if (ImGui.InputTextWithHint($"###{MemoizedId.Create(uniq: $"{kind}{i}")}", $"Enter {kind} here...",
                        ref str, 100))
                {
                    listItems[i].Text = str;
                    this.config.Save();
                }

                ImGui.SameLine();
                TriggerComponents.ToggleIsRegex(
                    $"Regex###{MemoizedId.Create(uniq: $"{kind}{i}")}",
                    listItems[i]);

                ImGui.SameLine();
                if (ImGui.Button($"Remove###{MemoizedId.Create(uniq: $"{kind}{i}")}"))
                {
                    listItems[i].ShouldRemove = true;
                }
            }

            for (var j = 0; j < listItems.Count; j++)
            {
                if (listItems[j].ShouldRemove)
                {
                    listItems.RemoveAt(j);
                    this.config.Save();
                }
            }

            if (ImGui.Button($"Add {kind}###{MemoizedId.Create(uniq: kind)}"))
            {
                listItems.Add(new Trigger(this.config));
            }
        }

        public void Dispose()
        {
            onPresetOpenRequested.Dispose();
        }
    }
}