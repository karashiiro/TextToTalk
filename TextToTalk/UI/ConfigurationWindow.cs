using Dalamud.CrystalTower.UI;
using Dalamud.Game.Text;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Speech.Synthesis;
using System.Text;

namespace TextToTalk.UI
{
    public class ConfigurationWindow : ImmediateModeWindow
    {
        public PluginConfiguration Configuration { get; set; }
        public WsServer WebSocketServer { get; set; }
        public SpeechSynthesizer Synthesizer { get; set; }

        public override void Draw(ref bool visible)
        {
            ImGui.SetNextWindowSize(new Vector2(520, 420));
            ImGui.Begin("TextToTalk Configuration", ref visible, ImGuiWindowFlags.NoResize);
            {
                if (ImGui.BeginTabBar("TextToTalk##tabbar"))
                {
                    if (ImGui.BeginTabItem("Synthesizer Settings"))
                    {
                        DrawSynthesizerSettings();
                        ImGui.EndTabItem();
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
            ImGui.End();
        }

        private void DrawSynthesizerSettings()
        {
            var useKeybind = Configuration.UseKeybind;
            if (ImGui.Checkbox("Enable Keybind", ref useKeybind))
            {
                Configuration.UseKeybind = useKeybind;
                Configuration.Save();
            }

            ImGui.PushItemWidth(100f);
            var kItem1 = VirtualKey.EnumToIndex(Configuration.ModifierKey);
            if (ImGui.Combo("##TextToTalkKeybind1", ref kItem1, VirtualKey.Names.Take(3).ToArray(), 3))
            {
                Configuration.ModifierKey = VirtualKey.IndexToEnum(kItem1);
                Configuration.Save();
            }
            ImGui.SameLine();
            var kItem2 = VirtualKey.EnumToIndex(Configuration.MajorKey) - 3;
            if (ImGui.Combo("TTS Toggle Keybind##TextToTalkKeybind2", ref kItem2, VirtualKey.Names.Skip(3).ToArray(), VirtualKey.Names.Length - 3))
            {
                Configuration.MajorKey = VirtualKey.IndexToEnum(kItem2) + 3;
                Configuration.Save();
            }
            ImGui.PopItemWidth();

            ImGui.Text("");
            var useWebsocket = Configuration.UseWebsocket;
            if (ImGui.Checkbox("Use WebSocket", ref useWebsocket))
            {
                Configuration.UseWebsocket = useWebsocket;
                Configuration.Save();

                if (Configuration.UseWebsocket)
                    WebSocketServer.Start();
                else
                    WebSocketServer.Stop();
            }
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), $"{(WebSocketServer.Active ? "Started" : "Will start")} on ws://localhost:{WebSocketServer.Port}");

            if (!useWebsocket)
            {
                var rate = Configuration.Rate;
                if (ImGui.SliderInt("Rate", ref rate, -10, 10))
                {
                    Configuration.Rate = rate;
                    Configuration.Save();
                }

                var volume = Configuration.Volume;
                if (ImGui.SliderInt("Volume", ref volume, 0, 100))
                {
                    Configuration.Volume = volume;
                    Configuration.Save();
                }

                var voiceName = Configuration.VoiceName;
                var voices = Synthesizer.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
                var voiceIndex = voices.FindIndex(iv => iv?.VoiceInfo?.Name == voiceName);
                if (ImGui.Combo("Voice",
                    ref voiceIndex,
                    voices
                        .Select(iv => $"{iv?.VoiceInfo?.Name} ({iv?.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language"})")
                        .ToArray(),
                    voices.Count))
                {
                    Configuration.VoiceName = voices[voiceIndex].VoiceInfo.Name;
                    Configuration.Save();
                }

                ImGui.Spacing();
                if (ImGui.Button("Don't see all of your voices?##VoiceUnlockerSuggestion"))
                {
                    OpenWindow<VoiceUnlockerWindow>();
                }
            }

            ImGui.Text("");
            var readFromQuestTalkAddon = Configuration.ReadFromQuestTalkAddon;
            if (ImGui.Checkbox("Read NPC dialogue from the dialogue window", ref readFromQuestTalkAddon))
            {
                Configuration.ReadFromQuestTalkAddon = readFromQuestTalkAddon;
                Configuration.Save();
            }

            ImGui.Text("");
            var nameNpcWithSay = Configuration.NameNpcWithSay;
            if (ImGui.Checkbox("Include \"NPC Name says:\" in NPC dialogue", ref nameNpcWithSay))
            {
                Configuration.NameNpcWithSay = nameNpcWithSay;
                Configuration.Save();
            }

            var disallowMultipleSay = Configuration.DisallowMultipleSay;
            if (ImGui.Checkbox("Only say \"Character Name says:\" the first time a character speaks", ref disallowMultipleSay))
            {
                Configuration.DisallowMultipleSay = disallowMultipleSay;
                Configuration.Save();
            }
        }

        private void DrawChannelSettings()
        {
            var currentConfiguration = Configuration.GetCurrentEnabledChatTypesPreset();

            var presets = Configuration.EnabledChatTypesPresets.ToList();
            presets.Sort(((a, b) => b.Id - a.Id));
            var presetIndex = presets.IndexOf(currentConfiguration);
            if (ImGui.Combo("Preset##TTT1", ref presetIndex, presets.Select(p => p.Name).ToArray(), presets.Count))
            {
                Configuration.CurrentPresetId = presets[presetIndex].Id;
                Configuration.Save();
            }

            if (ImGui.Button("New preset#TTT2"))
            {
                var newPreset = Configuration.NewChatTypesPreset();
                var presetModificationWindow = GetWindow<PresetModificationWindow>();
                presetModificationWindow.PresetId = newPreset.Id;
                OpenWindow<PresetModificationWindow>();
            }

            ImGui.SameLine();

            if (ImGui.Button("Edit#TTT3"))
            {
                var presetModificationWindow = GetWindow<PresetModificationWindow>();
                presetModificationWindow.PresetId = currentConfiguration.Id;
                OpenWindow<PresetModificationWindow>();
            }

            ImGui.SameLine();

            if (Configuration.EnabledChatTypesPresets.Count > 1 && ImGui.Button("Delete#TTT4"))
            {
                var otherPreset = Configuration.EnabledChatTypesPresets.First(p => p.Id != currentConfiguration.Id);
                Configuration.SetCurrentEnabledChatTypesPreset(otherPreset.Id);
                Configuration.EnabledChatTypesPresets.Remove(currentConfiguration);
            }

            ImGui.Spacing();

            var enableAll = currentConfiguration.EnableAllChatTypes;
            if (ImGui.Checkbox("Enable all (including undocumented)", ref enableAll))
            {
                currentConfiguration.EnableAllChatTypes = enableAll;
            }
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), "Recommended for trigger use");
            if (enableAll) return;

            var channels = Enum.GetNames(typeof(XivChatType)).Concat(Enum.GetNames(typeof(AdditionalChatTypes.Enum)));
            foreach (var channel in channels)
            {
                XivChatType enumValue;
                try
                {
                    enumValue = (XivChatType)Enum.Parse(typeof(XivChatType), channel);
                }
                catch (ArgumentException)
                {
                    enumValue = (XivChatType)(int)Enum.Parse(typeof(AdditionalChatTypes.Enum), channel);
                }

                var selected = currentConfiguration.EnabledChatTypes.Contains((int)enumValue);
                if (!ImGui.Checkbox(channel == "PvPTeam" ? "PvP Team" : SplitWords(channel), ref selected)) continue;
                var inEnabled = currentConfiguration.EnabledChatTypes.Contains((int)enumValue);
                if (inEnabled)
                {
                    currentConfiguration.EnabledChatTypes.Remove((int)enumValue);
                    Configuration.Save();
                }
                else
                {
                    currentConfiguration.EnabledChatTypes.Add((int)enumValue);
                    Configuration.Save();
                }
            }
        }

        private static string SplitWords(string oneWord)
        {
            var words = oneWord
                .Select(c => c)
                .Skip(1)
                .Aggregate("" + oneWord[0], (acc, c) => acc + (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' ? " " + c : "" + c))
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
            var currentConfiguration = Configuration.GetCurrentEnabledChatTypesPreset();

            var enableAll = currentConfiguration.EnableAllChatTypes;
            if (ImGui.Checkbox("Enable all chat types (including undocumented)", ref enableAll))
            {
                currentConfiguration.EnableAllChatTypes = enableAll;
            }
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), "Recommended for trigger use");
            ImGui.Dummy(new Vector2(0, 5));

            ExpandyList("Trigger", Configuration.Good);
            ExpandyList("Exclusion", Configuration.Bad);
        }

        private void ExpandyList(string kind, IList<Trigger> listItems)
        {
            ImGui.Text($"{kind}s");

            for (var i = 0; i < listItems.Count; i++)
            {
                var str = listItems[i].Text;
                if (ImGui.InputTextWithHint($"###TextToTalk{kind}{i}", $"Enter {kind} here...", ref str, 100))
                {
                    listItems[i].Text = str;
                    Configuration.Save();
                }

                ImGui.SameLine();
                var isRegex = listItems[i].IsRegex;
                if (ImGui.Checkbox($"Regex###TextToTalkRegex{kind}{i}", ref isRegex))
                {
                    listItems[i].IsRegex = isRegex;
                    Configuration.Save();
                }

                ImGui.SameLine();
                if (ImGui.Button($"Remove###TextToTalkRemove{kind}{i}"))
                {
                    listItems[i].ShouldRemove = true;
                }
            }

            for (var j = 0; j < listItems.Count; j++)
            {
                if (listItems[j].ShouldRemove)
                {
                    listItems.RemoveAt(j);
                    Configuration.Save();
                }
            }

            if (ImGui.Button($"Add {kind}"))
            {
                listItems.Add(new Trigger());
            }
        }
    }
}