using Dalamud.Game.Text;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Speech.Synthesis;
using System.Text;

namespace TextToTalk
{
    public class PluginUI
    {
        private static readonly SpeechSynthesizer DummySynthesizer = new SpeechSynthesizer();

        private readonly PluginConfiguration config;
        private readonly WsServer wsServer;
        private bool configVisible;

        public bool ConfigVisible
        {
            get => this.configVisible;
            set => this.configVisible = value;
        }

        public PluginUI(PluginConfiguration config, WsServer wsServer)
        {
            this.config = config;
            this.wsServer = wsServer;
        }

        public void DrawConfig()
        {
            if (!ConfigVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(520, 400));
            ImGui.Begin("TextToTalk Configuration", ref this.configVisible, ImGuiWindowFlags.NoResize);
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
            var useWebsocket = this.config.UseWebsocket;
            if (ImGui.Checkbox("Use WebSocket", ref useWebsocket))
            {
                this.config.UseWebsocket = useWebsocket;

                if (this.config.UseWebsocket)
                    this.wsServer.Start();
                else
                    this.wsServer.Stop();
            }
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), $"{(this.wsServer.Active ? "Started" : "Will start")} on ws://localhost:{this.wsServer.Port}");
            if (useWebsocket) return;

            var rate = this.config.Rate;
            if (ImGui.SliderInt("Rate", ref rate, -10, 10))
            {
                this.config.Rate = rate;
                this.config.Save();
            }

            var volume = this.config.Volume;
            if (ImGui.SliderInt("Volume", ref volume, 0, 100))
            {
                this.config.Volume = volume;
                this.config.Save();
            }

            var voiceName = this.config.VoiceName;
            var voices = DummySynthesizer.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
            var voiceIndex = voices.FindIndex(iv => iv?.VoiceInfo?.Name == voiceName);
            if (ImGui.Combo("Voice",
                            ref voiceIndex,
                            voices
                                .Select(iv => $"{iv?.VoiceInfo?.Name} ({iv?.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language"})")
                                .ToArray(),
                            voices.Count))
            {
                this.config.VoiceName = voices[voiceIndex].VoiceInfo.Name;
            }

            ImGui.Text(""); // Empty line
            var nameNpcWithSay = this.config.NameNpcWithSay;
            if (ImGui.Checkbox("Include \"NPC Name says:\" in NPC dialogue", ref nameNpcWithSay))
            {
                this.config.NameNpcWithSay = nameNpcWithSay;
                this.config.Save();
            }
        }

        private void DrawChannelSettings()
        {
            var enableAll = this.config.EnableAllChatTypes;
            if (ImGui.Checkbox("Enable all (including undocumented)", ref enableAll))
            {
                this.config.EnableAllChatTypes = enableAll;
            }
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.6f), "Recommended for trigger use");
            if (enableAll) return;

            var channels = Enum.GetNames(typeof(XivChatType)).Concat(Enum.GetNames(typeof(AdditionalChatTypes.Enum)));
            foreach (var channel in channels)
            {
                XivChatType enumValue;
                try
                {
                    enumValue = (XivChatType) Enum.Parse(typeof(XivChatType), channel);
                }
                catch (ArgumentException)
                {
                    enumValue = (XivChatType) (int) Enum.Parse(typeof(AdditionalChatTypes.Enum), channel);
                }

                var selected = this.config.EnabledChatTypes.Contains((int)enumValue);
                if (!ImGui.Checkbox(channel == "PvPTeam" ? "PvP Team" : SplitWords(channel), ref selected)) continue;
                var inEnabled = this.config.EnabledChatTypes.Contains((int)enumValue);
                if (inEnabled)
                {
                    this.config.EnabledChatTypes.Remove((int)enumValue);
                    this.config.Save();
                }
                else
                {
                    this.config.EnabledChatTypes.Add((int)enumValue);
                    this.config.Save();
                }
            }
        }

        private static string SplitWords(string oneWord)
        {
            var words = oneWord
                .Select(c => c)
                .Skip(1)
                .Aggregate("" + oneWord[0], (acc, c) => acc + (c >= 'A' && c <= 'Z' || c >= '0' && c <='9' ? " " + c : "" + c))
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
            var enableAll = this.config.EnableAllChatTypes;
            if (ImGui.Checkbox("Enable all chat types (including undocumented)", ref enableAll))
            {
                this.config.EnableAllChatTypes = enableAll;
            }
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
                if (ImGui.InputTextWithHint($"###TextToTalk{kind}{i}", $"Enter {kind} here...", ref str, 100))
                {
                    listItems[i].Text = str;
                    this.config.Save();
                }
                
                ImGui.SameLine();
                var isRegex = listItems[i].IsRegex;
                if (ImGui.Checkbox($"Regex###TextToTalkRegex{kind}{i}", ref isRegex))
                {
                    listItems[i].IsRegex = isRegex;
                    this.config.Save();
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
                    this.config.Save();
                }
            }

            if (ImGui.Button($"Add {kind}"))
            {
                listItems.Add(new Trigger());
            }
        }
    }
}