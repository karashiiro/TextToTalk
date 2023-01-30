using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using Dalamud.Logging;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI;
using TextToTalk.UI.Lexicons;

namespace TextToTalk.Backends.System;

public class SystemBackendUI
{
    private static readonly Lazy<SpeechSynthesizer> DummySynthesizer = new(() =>
    {
        try
        {
            return new SpeechSynthesizer();
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Failed to create speech synthesizer.");
            return null;
        }
    });

    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly ConcurrentQueue<SelectVoiceFailedException> selectVoiceFailures;

    public SystemBackendUI(PluginConfiguration config, LexiconManager lexiconManager,
        ConcurrentQueue<SelectVoiceFailedException> selectVoiceFailures, HttpClient http)
    {
        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");

        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.config = config;
        this.lexiconComponent = new LexiconComponent(lexiconManager, lexiconRepository, config, () => config.Lexicons);
        this.selectVoiceFailures = selectVoiceFailures;
    }

    private readonly IDictionary<string, Exception> voiceExceptions = new Dictionary<string, Exception>();

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        ImGui.TextColored(BackendUI.HintColor, "This TTS provider is only supported on Windows.");

        if (this.selectVoiceFailures.TryDequeue(out var e1))
        {
            this.voiceExceptions[e1.VoiceId] = e1;
        }

        var currentVoicePreset = this.config.GetCurrentVoicePreset<SystemVoicePreset>();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.System).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any())
        {
            var presetIndex = currentVoicePreset is not null ? presets.IndexOf(currentVoicePreset) : -1;
            if (ImGui.Combo($"Preset##{MemoizedId.Create()}", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                    presets.Count))
            {
                this.config.SetCurrentVoicePreset(presets[presetIndex].Id);
                this.config.Save();
            }
        }
        else
        {
            ImGui.TextColored(BackendUI.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }

        BackendUI.NewPresetButton<SystemVoicePreset>($"New preset##{MemoizedId.Create()}", this.config);

        if (!presets.Any() || currentVoicePreset is null)
        {
            return;
        }

        ImGui.SameLine();
        BackendUI.DeletePresetButton(
            $"Delete preset##{MemoizedId.Create()}",
            currentVoicePreset,
            TTSBackend.System,
            this.config);

        var presetName = currentVoicePreset.Name;
        if (ImGui.InputText($"Preset name##{MemoizedId.Create()}", ref presetName, 64))
        {
            currentVoicePreset.Name = presetName;
            this.config.Save();
        }

        var rate = currentVoicePreset.Rate;
        if (ImGui.SliderInt($"Rate##{MemoizedId.Create()}", ref rate, -10, 10))
        {
            currentVoicePreset.Rate = rate;
            this.config.Save();
        }

        var volume = currentVoicePreset.Volume;
        if (ImGui.SliderInt($"Volume##{MemoizedId.Create()}", ref volume, 0, 100))
        {
            currentVoicePreset.Volume = volume;
            this.config.Save();
        }

        var voiceName = currentVoicePreset.VoiceName;
        var voices = DummySynthesizer.Value != null
            ? DummySynthesizer.Value.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList()
            : new List<InstalledVoice>();
        if (voices.Any())
        {
            var voicesUi = voices.Select(FormatVoiceInfo).ToArray();
            var voiceIndex = voices.FindIndex(iv => iv.VoiceInfo?.Name == voiceName);
            if (ImGui.Combo($"Voice##{MemoizedId.Create()}", ref voiceIndex, voicesUi, voices.Count))
            {
                this.voiceExceptions.Remove(voices[voiceIndex].VoiceInfo.Name);
                currentVoicePreset.VoiceName = voices[voiceIndex].VoiceInfo.Name;
                this.config.Save();
            }

            if (this.voiceExceptions.TryGetValue(voiceName, out var e2))
            {
                PrintVoiceExceptions(e2);
            }
        }

        if (ImGui.Button($"Don't see all of your voices?##{MemoizedId.Create()}"))
        {
            helpers.OpenVoiceUnlocker();
        }

        this.lexiconComponent.Draw();

        ImGui.Spacing();

        var useGenderedVoicePresets = this.config.UseGenderedVoicePresets;
        if (ImGui.Checkbox($"Use gendered voice presets##{MemoizedId.Create()}", ref useGenderedVoicePresets))
        {
            this.config.UseGenderedVoicePresets = useGenderedVoicePresets;
            this.config.Save();
        }

        if (useGenderedVoicePresets)
        {
            var voiceConfig = this.config.GetVoiceConfig();

            if (BackendUI.ImGuiPresetCombo($"Ungendered preset(s)##{MemoizedId.Create()}",
                    voiceConfig.GetUngenderedPresets(TTSBackend.System), presets))
            {
                this.config.Save();
            }

            if (BackendUI.ImGuiPresetCombo($"Male preset(s)##{MemoizedId.Create()}",
                    voiceConfig.GetMalePresets(TTSBackend.System), presets))
            {
                this.config.Save();
            }

            if (BackendUI.ImGuiPresetCombo($"Female preset(s)##{MemoizedId.Create()}",
                    voiceConfig.GetFemalePresets(TTSBackend.System), presets))
            {
                this.config.Save();
            }

            BackendUI.ImGuiMultiVoiceHint();
        }
    }

    private static void PrintVoiceExceptions(Exception e)
    {
        if (e.InnerException != null)
        {
            ImGui.TextColored(BackendUI.Red, $"Voice errors:\n  {e.Message}");
            PrintVoiceExceptionsR(e.InnerException);
        }
        else
        {
            ImGui.TextColored(BackendUI.Red, $"Voice error:\n  {e.Message}");
        }
    }

    private static void PrintVoiceExceptionsR(Exception e)
    {
        do
        {
            ImGui.TextColored(BackendUI.Red, $"  {e.Message}");
        } while (e.InnerException != null);
    }

    private static string FormatVoiceInfo(InstalledVoice iv)
    {
        var line = new StringBuilder(iv.VoiceInfo?.Name ?? "");
        line.Append(" (")
            .Append(iv.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language")
            .Append(")");

        if (iv.VoiceInfo?.Name.Contains("Zira") ?? false)
        {
            line.Append(" [UNSTABLE]");
        }

        return line.ToString();
    }
}