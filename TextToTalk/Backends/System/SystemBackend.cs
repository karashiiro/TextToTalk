using Dalamud.Plugin;
using ImGuiNET;
using System.Linq;
using System.Speech.Synthesis;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends.System
{
    public class SystemBackend : VoiceBackend
    {
        private static readonly SpeechSynthesizer DummySynthesizer = new();

        private readonly PluginConfiguration config;
        private readonly SoundQueue soundQueue;

        public SystemBackend(PluginConfiguration config)
        {
            this.config = config;
            this.soundQueue = new SoundQueue();
        }

        public override void Say(Gender gender, string text)
        {
            var voicePreset = gender switch
            {
                Gender.Male => this.config.GetCurrentMaleVoicePreset(),
                Gender.Female => this.config.GetCurrentFemaleVoicePreset(),
                _ => this.config.GetCurrentUngenderedVoicePreset(),
            };

            this.soundQueue.EnqueueSound(voicePreset, text);
        }

        public override void CancelSay()
        {
            this.soundQueue.CancelAllSounds();
            PluginLog.Log("Canceled SpeechSynthesizer TTS.");
        }

        public override void DrawSettings(ImExposedFunctions helpers)
        {
            var currentVoicePreset = this.config.GetCurrentVoicePreset();

            var presets = this.config.VoicePresets.ToList();
            presets.Sort((a, b) => a.Id - b.Id);

            var presetIndex = presets.IndexOf(currentVoicePreset);
            if (ImGui.Combo("Preset##TTTVoice3", ref presetIndex, presets.Select(p => p.Name).ToArray(), presets.Count))
            {
                this.config.CurrentVoicePresetId = presets[presetIndex].Id;
                this.config.Save();
            }

            if (ImGui.Button("New preset##TTTVoice4"))
            {
                var newPreset = this.config.NewVoicePreset();
                this.config.SetCurrentVoicePreset(newPreset.Id);
            }

            if (this.config.EnabledChatTypesPresets.Count > 1)
            {
                ImGui.SameLine();
                if (ImGui.Button("Delete##TTTVoice5"))
                {
                    var otherPreset = this.config.VoicePresets.First(p => p.Id != currentVoicePreset.Id);
                    this.config.SetCurrentVoicePreset(otherPreset.Id);
                    this.config.VoicePresets.Remove(currentVoicePreset);
                }
            }

            var rate = currentVoicePreset.Rate;
            if (ImGui.SliderInt("Rate##TTTVoice6", ref rate, -10, 10))
            {
                currentVoicePreset.Rate = rate;
                this.config.Save();
            }

            var volume = currentVoicePreset.Volume;
            if (ImGui.SliderInt("Volume##TTTVoice7", ref volume, 0, 100))
            {
                currentVoicePreset.Volume = volume;
                this.config.Save();
            }

            var voiceName = currentVoicePreset.VoiceName;
            var voices = DummySynthesizer.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
            var voiceIndex = voices.FindIndex(iv => iv?.VoiceInfo?.Name == voiceName);
            if (ImGui.Combo("Voice##TTTVoice8",
                ref voiceIndex,
                voices
                    .Select(iv => $"{iv?.VoiceInfo?.Name} ({iv?.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language"})")
                    .ToArray(),
                voices.Count))
            {
                currentVoicePreset.VoiceName = voices[voiceIndex].VoiceInfo.Name;
                this.config.Save();
            }

            if (ImGui.Button("Don't see all of your voices?##VoiceUnlockerSuggestion"))
            {
                helpers.OpenVoiceUnlockerWindow?.Invoke();
            }

            ImGui.Spacing();

            var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
            if (ImGui.Checkbox("Use gendered voice presets##TTTVoice9", ref useGenderedVoicePresets))
            {
                this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
                this.config.Save();
            }

            if (useGenderedVoicePresets)
            {
                var currentUngenderedVoicePreset = this.config.GetCurrentUngenderedVoicePreset();
                var currentMaleVoicePreset = this.config.GetCurrentMaleVoicePreset();
                var currentFemaleVoicePreset = this.config.GetCurrentFemaleVoicePreset();

                var presetArray = presets.Select(p => p.Name).ToArray();

                var ungenderedPresetIndex = presets.IndexOf(currentUngenderedVoicePreset);
                if (ImGui.Combo("Ungendered preset##TTTVoice12", ref ungenderedPresetIndex, presetArray, presets.Count))
                {
                    this.config.UngenderedVoicePresetId = presets[ungenderedPresetIndex].Id;
                    this.config.Save();
                }

                var malePresetIndex = presets.IndexOf(currentMaleVoicePreset);
                if (ImGui.Combo("Male preset##TTTVoice10", ref malePresetIndex, presetArray, presets.Count))
                {
                    this.config.MaleVoicePresetId = presets[malePresetIndex].Id;
                    this.config.Save();
                }

                var femalePresetIndex = presets.IndexOf(currentFemaleVoicePreset);
                if (ImGui.Combo("Female preset##TTTVoice11", ref femalePresetIndex, presetArray, presets.Count))
                {
                    this.config.FemaleVoicePresetId = presets[femalePresetIndex].Id;
                    this.config.Save();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.soundQueue.Dispose();
            }
        }
    }
}