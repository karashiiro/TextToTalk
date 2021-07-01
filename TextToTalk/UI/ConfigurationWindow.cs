using Dalamud.CrystalTower.UI;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Speech.Synthesis;
using System.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.UI
{
    public class ConfigurationWindow : ImmediateModeWindow
    {
        public PluginConfiguration Configuration { get; set; }
        public WSServer WebSocketServer { get; set; }
        public SpeechSynthesizer Synthesizer { get; set; }

        public override void Draw(ref bool visible)
        {
            ImGui.SetNextWindowSize(new Vector2(520, 480));
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
            if (ImGui.CollapsingHeader("Keybinds##TextToTalkKeybind1"))
            {
                var useKeybind = Configuration.UseKeybind;
                if (ImGui.Checkbox("Enable Keybind##TextToTalkKeybind2", ref useKeybind))
                {
                    Configuration.UseKeybind = useKeybind;
                    Configuration.Save();
                }

                ImGui.PushItemWidth(100f);
                var kItem1 = VirtualKey.EnumToIndex(Configuration.ModifierKey);
                if (ImGui.Combo("##TextToTalkKeybind3", ref kItem1, VirtualKey.Names.Take(3).ToArray(), 3))
                {
                    Configuration.ModifierKey = VirtualKey.IndexToEnum(kItem1);
                    Configuration.Save();
                }
                ImGui.SameLine();
                var kItem2 = VirtualKey.EnumToIndex(Configuration.MajorKey) - 3;
                if (ImGui.Combo("TTS Toggle Keybind##TextToTalkKeybind4", ref kItem2, VirtualKey.Names.Skip(3).ToArray(), VirtualKey.Names.Length - 3))
                {
                    Configuration.MajorKey = VirtualKey.IndexToEnum(kItem2 + 3);
                    Configuration.Save();
                }
                ImGui.PopItemWidth();
            }

            if (ImGui.CollapsingHeader("Voices##TTTVoice1", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var backends = Enum.GetNames(typeof(TTSBackend)).Select(SplitWords).ToArray();
                var backend = Configuration.Backend;
                var backendIndex = Array.IndexOf(backends, backend.ToString());

                if (ImGui.Combo("Voice backend##TTTVoice2", ref backendIndex, backends, backends.Length))
                {
                    if (Enum.TryParse(backends[backendIndex].Replace(" ", ""), out TTSBackend newBackend))
                    {
                        Configuration.Backend = newBackend;
                        Configuration.Save();

                        if (Configuration.Backend == TTSBackend.Websocket)
                        {
                            WebSocketServer.Start();
                        }
                        else
                        {
                            WebSocketServer.Stop();
                        }
                    }
                    else
                    {
                        PluginLog.Error($"Failed to parse TTS backend \"{backends[backendIndex]}\".");
                    }
                }

                if (Configuration.Backend == TTSBackend.Websocket)
                {
                    var port = Configuration.WebsocketPort;
                    var portBytes = Encoding.UTF8.GetBytes(port.ToString());
                    var inputBuffer = new byte[6]; // One extra byte for the null terminator
                    Array.Copy(portBytes, inputBuffer, portBytes.Length > inputBuffer.Length ? inputBuffer.Length : portBytes.Length);

                    if (ImGui.InputText("Port##TTTVoice12", inputBuffer, (uint)inputBuffer.Length, ImGuiInputTextFlags.CharsDecimal))
                    {
                        if (int.TryParse(Encoding.UTF8.GetString(inputBuffer), out var newPort))
                        {
                            try
                            {
                                WebSocketServer.RestartWithPort(newPort);
                                Configuration.WebsocketPort = newPort;
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Port out of range");
                            }
                            catch (SocketException)
                            {
                                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Port already taken");
                            }
                        }
                        else
                        {
                            PluginLog.LogError("Failed to parse port!");
                        }
                    }

                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), $"{(WebSocketServer.Active ? "Started" : "Will start")} on ws://localhost:{WebSocketServer.Port}");

                    ImGui.Spacing();

                    if (ImGui.Button("Restart server##TTTVoice13"))
                    {
                        WebSocketServer.RestartWithPort(Configuration.WebsocketPort);
                    }
                }
                else
                {
                    var currentVoicePreset = Configuration.GetCurrentVoicePreset();

                    var presets = Configuration.VoicePresets.ToList();
                    presets.Sort((a, b) => a.Id - b.Id);

                    var presetIndex = presets.IndexOf(currentVoicePreset);
                    if (ImGui.Combo("Preset##TTTVoice3", ref presetIndex, presets.Select(p => p.Name).ToArray(), presets.Count))
                    {
                        Configuration.CurrentVoicePresetId = presets[presetIndex].Id;
                        Configuration.Save();
                    }

                    if (ImGui.Button("New preset##TTTVoice4"))
                    {
                        var newPreset = Configuration.NewVoicePreset();
                        Configuration.SetCurrentVoicePreset(newPreset.Id);
                    }

                    if (Configuration.EnabledChatTypesPresets.Count > 1)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("Delete##TTTVoice5"))
                        {
                            var otherPreset = Configuration.VoicePresets.First(p => p.Id != currentVoicePreset.Id);
                            Configuration.SetCurrentVoicePreset(otherPreset.Id);
                            Configuration.VoicePresets.Remove(currentVoicePreset);
                        }
                    }

                    var rate = currentVoicePreset.Rate;
                    if (ImGui.SliderInt("Rate##TTTVoice6", ref rate, -10, 10))
                    {
                        currentVoicePreset.Rate = rate;
                        Configuration.Save();
                    }

                    var volume = currentVoicePreset.Volume;
                    if (ImGui.SliderInt("Volume##TTTVoice7", ref volume, 0, 100))
                    {
                        currentVoicePreset.Volume = volume;
                        Configuration.Save();
                    }

                    var voiceName = currentVoicePreset.VoiceName;
                    var voices = Synthesizer.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
                    var voiceIndex = voices.FindIndex(iv => iv?.VoiceInfo?.Name == voiceName);
                    if (ImGui.Combo("Voice##TTTVoice8",
                        ref voiceIndex,
                        voices
                            .Select(iv => $"{iv?.VoiceInfo?.Name} ({iv?.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language"})")
                            .ToArray(),
                        voices.Count))
                    {
                        currentVoicePreset.VoiceName = voices[voiceIndex].VoiceInfo.Name;
                        Configuration.Save();
                    }

                    ImGui.Spacing();

                    var useGenderedVoicePresets = Configuration.UseGenderedVoicePresets;
                    if (ImGui.Checkbox("Use gendered voice presets##TTTVoice9", ref useGenderedVoicePresets))
                    {
                        Configuration.UseGenderedVoicePresets = useGenderedVoicePresets;
                        Configuration.Save();
                    }

                    if (useGenderedVoicePresets)
                    {
                        var currentMaleVoicePreset = Configuration.GetCurrentMaleVoicePreset();
                        var currentFemaleVoicePreset = Configuration.GetCurrentFemaleVoicePreset();

                        var malePresetIndex = presets.IndexOf(currentMaleVoicePreset);
                        if (ImGui.Combo("Male preset##TTTVoice10", ref malePresetIndex, presets.Select(p => p.Name).ToArray(), presets.Count))
                        {
                            Configuration.MaleVoicePresetId = presets[malePresetIndex].Id;
                            Configuration.Save();
                        }

                        var femalePresetIndex = presets.IndexOf(currentFemaleVoicePreset);
                        if (ImGui.Combo("Female preset##TTTVoice11", ref femalePresetIndex, presets.Select(p => p.Name).ToArray(), presets.Count))
                        {
                            Configuration.FemaleVoicePresetId = presets[femalePresetIndex].Id;
                            Configuration.Save();
                        }
                    }

                    ImGui.Spacing();
                    if (ImGui.Button("Don't see all of your voices?##VoiceUnlockerSuggestion"))
                    {
                        OpenWindow<VoiceUnlockerWindow>();
                    }
                }
            }

            if (ImGui.CollapsingHeader("Dialogue"))
            {
                var readFromQuestTalkAddon = Configuration.ReadFromQuestTalkAddon;
                if (ImGui.Checkbox("Read NPC dialogue from the dialogue window", ref readFromQuestTalkAddon))
                {
                    Configuration.ReadFromQuestTalkAddon = readFromQuestTalkAddon;
                    Configuration.Save();
                }

                ImGui.Spacing();
                var enableNameWithSay = Configuration.EnableNameWithSay;
                if (ImGui.Checkbox("Enable \"X says:\" when people speak", ref enableNameWithSay))
                {
                    Configuration.EnableNameWithSay = enableNameWithSay;
                    Configuration.Save();
                }

                if (enableNameWithSay)
                {
                    ImGui.Spacing();
                    ImGui.Indent();
                    var nameNpcWithSay = Configuration.NameNpcWithSay;
                    if (ImGui.Checkbox("Also say \"NPC Name says:\" in NPC dialogue", ref nameNpcWithSay))
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
            }
        }

        private void DrawChannelSettings()
        {
            var currentEnabledChatTypesPreset = Configuration.GetCurrentEnabledChatTypesPreset();

            var presets = Configuration.EnabledChatTypesPresets.ToList();
            presets.Sort((a, b) => a.Id - b.Id);
            var presetIndex = presets.IndexOf(currentEnabledChatTypesPreset);
            if (ImGui.Combo("Preset##TTT1", ref presetIndex, presets.Select(p => p.Name).ToArray(), presets.Count))
            {
                Configuration.CurrentPresetId = presets[presetIndex].Id;
                Configuration.Save();
            }

            if (ImGui.Button("New preset##TTT2"))
            {
                var newPreset = Configuration.NewChatTypesPreset();
                Configuration.SetCurrentEnabledChatTypesPreset(newPreset.Id);
                OpenWindow<PresetModificationWindow>();
            }

            ImGui.SameLine();

            if (ImGui.Button("Edit##TTT3"))
            {
                OpenWindow<PresetModificationWindow>();
            }

            if (Configuration.EnabledChatTypesPresets.Count > 1)
            {
                ImGui.SameLine();
                if (ImGui.Button("Delete##TTT4"))
                {
                    var otherPreset = Configuration.EnabledChatTypesPresets.First(p => p.Id != currentEnabledChatTypesPreset.Id);
                    Configuration.SetCurrentEnabledChatTypesPreset(otherPreset.Id);
                    Configuration.EnabledChatTypesPresets.Remove(currentEnabledChatTypesPreset);
                }
            }

            ImGui.Spacing();

            var enableAll = currentEnabledChatTypesPreset.EnableAllChatTypes;
            if (ImGui.Checkbox("Enable all (including undocumented)", ref enableAll))
            {
                currentEnabledChatTypesPreset.EnableAllChatTypes = enableAll;
            }
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), "Recommended for trigger use");
            if (enableAll) return;

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

                var selected = currentEnabledChatTypesPreset.EnabledChatTypes.Contains((int)enumValue);
                if (!ImGui.Checkbox(channel == "PvPTeam" ? "PvP Team" : SplitWords(channel), ref selected)) continue;
                var inEnabled = currentEnabledChatTypesPreset.EnabledChatTypes.Contains((int)enumValue);
                if (inEnabled)
                {
                    currentEnabledChatTypesPreset.EnabledChatTypes.Remove((int)enumValue);
                    Configuration.Save();
                }
                else
                {
                    currentEnabledChatTypesPreset.EnabledChatTypes.Add((int)enumValue);
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
                Configuration.Save();
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