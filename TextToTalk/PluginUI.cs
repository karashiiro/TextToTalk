using Dalamud.Game.Chat;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Speech.Synthesis;

namespace TextToTalk
{
    public class PluginUI
    {
        private readonly PluginConfiguration config;

        public bool ConfigVisible { get; set; }

        public PluginUI(PluginConfiguration config)
        {
            this.config = config;
        }

        public void DrawConfig()
        {
            if (!ConfigVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(520, 400));
            ImGui.Begin("TextToTalk Configuration", ImGuiWindowFlags.NoResize);
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
                }

                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        private void DrawSynthesizerSettings()
        {
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

            var gender = this.config.GenderIndex;
            var voiceGenders = Enum.GetNames(typeof(VoiceGender));
            if (ImGui.Combo("Voice Gender (from installed)", ref gender, voiceGenders, voiceGenders.Length))
            {
                this.config.GenderIndex = gender;
            }

            var age = this.config.AgeIndex;
            var voiceAges = Enum.GetNames(typeof(VoiceAge));
            if (ImGui.Combo("Voice Age (from installed)", ref age, voiceAges, voiceAges.Length))
            {
                this.config.AgeIndex = age;
            }
        }

        private void DrawChannelSettings()
        {
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
            return oneWord
                .Select(c => c)
                .Skip(1)
                .Aggregate("" + oneWord[0], (acc, c) => acc + (c >= 'A' && c <= 'Z' || c >= '0' && c <='9' ? " " + c : "" + c));
        }
    }
}