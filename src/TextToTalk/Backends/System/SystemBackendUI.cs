using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Text;
using Google.Api;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI;
using TextToTalk.UI.Lexicons;

namespace TextToTalk.Backends.System;

public class SystemBackendUI
{
    private readonly SystemBackendUIModel model;
    private readonly PluginConfiguration config;
    private readonly LexiconComponent lexiconComponent;
    private readonly SystemBackend backend;

    public SystemBackendUI(SystemBackendUIModel model, PluginConfiguration config, LexiconManager lexiconManager,
        HttpClient http, SystemBackend backend)
    {
        // TODO: Make this configurable
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var downloadPath = Path.Join(appData, "TextToTalk");

        var lexiconRepository = new LexiconRepository(http, downloadPath);

        this.backend = backend;
        this.model = model;
        this.config = config;
        this.lexiconComponent = new LexiconComponent(lexiconManager, lexiconRepository, config, () => config.Lexicons);
    }

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        ImGui.TextColored(ImColor.HintColor, "This TTS provider is only supported on Windows.");

        var currentVoicePreset = this.config.GetCurrentVoicePreset<SystemVoicePreset>();

        var presets = this.config.GetVoicePresetsForBackend(TTSBackend.System).ToList();
        presets.Sort((a, b) => a.Id - b.Id);

        if (presets.Any() && currentVoicePreset != null)
        {
            var presetIndex = currentVoicePreset is not null ? presets.IndexOf(currentVoicePreset) : -1;
            if (ImGui.Combo($"Preset##{MemoizedId.Create()}", ref presetIndex, presets.Select(p => p.Name).ToArray(),
                    presets.Count))
            {
                this.config.SetCurrentVoicePreset(presets[presetIndex].Id);
                this.config.Save();
            }
        }
        else if (currentVoicePreset != null)
        {
            ImGui.TextColored(ImColor.Red, "You have no presets. Please create one using the \"New preset\" button.");
        }
        else if (currentVoicePreset == null && presets.Count > 0)
        {
            config.SetCurrentVoicePreset(presets.First().Id);
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
        var voices = this.model.GetInstalledVoices();
        if (voices.Any())
        {
            var voicesUi = voices.Select(FormatVoiceInfo).ToArray();
            var voiceIndex = voices.FindIndex(iv => iv.VoiceInfo?.Name == voiceName);
            if (ImGui.Combo($"Voice##{MemoizedId.Create()}", ref voiceIndex, voicesUi, voices.Count))
            {
                this.model.DismissVoiceException(voices[voiceIndex].VoiceInfo.Name);
                currentVoicePreset.VoiceName = voices[voiceIndex].VoiceInfo.Name;
                this.config.Save();
            }

            if (this.model.VoiceExceptions.TryGetValue(voiceName ?? "", out var e2))
            {
                PrintVoiceExceptions(e2);
            }
        }

        if (ImGui.Button($"Don't see all of your voices?##{MemoizedId.Create()}"))
        {
            helpers.OpenVoiceUnlocker();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Also check out NaturalVoiceSAPIAdapter!##{MemoizedId.Create()}"))
        {
            WebBrowser.Open("https://github.com/gexgd0419/NaturalVoiceSAPIAdapter");
        }
        if (ImGui.Button($"Test##{MemoizedId.Create()}"))
        {
            var voice = currentVoicePreset;
            if (voice is not null)
            {
                var request = new SayRequest
                {
                    Source = TextSource.Chat,
                    Voice = currentVoicePreset,
                    Speaker = "Speaker",
                    Text = $"Hello from Windows Narrator, this is a test message",
                    TextTemplate = "Hello from Windows Narrator, this is a test message",
                    Race = "Hyur",
                    BodyType = GameEnums.BodyType.Adult,
                    ChatType = XivChatType.Say,
                    Language = ClientLanguage.English,
                };
                backend.CancelSay(TextSource.Chat);
                backend.Say(request);
            }
        }

        this.lexiconComponent.Draw();

        ImGui.Spacing();

        ConfigComponents.ToggleUseGenderedVoicePresets(
            $"Use gendered voice presets##{MemoizedId.Create()}",
            this.config);

        if (this.config.UseGenderedVoicePresets)
        {
            BackendUI.GenderedPresetConfig("System", TTSBackend.System, this.config, presets);
        }
    }

    private static void PrintVoiceExceptions(Exception e)
    {
        if (e.InnerException != null)
        {
            ImGui.TextColored(ImColor.Red, $"Voice errors:\n  {e.Message}");
            PrintVoiceExceptionsR(e.InnerException);
        }
        else
        {
            ImGui.TextColored(ImColor.Red, $"Voice error:\n  {e.Message}");
        }
    }

    private static void PrintVoiceExceptionsR(Exception? e)
    {
        if (e == null)
        {
            return;
        }

        do
        {
            ImGui.TextColored(ImColor.Red, $"  {e.Message}");
            e = e.InnerException;
        } while (e?.InnerException != null);
    }

    private static string FormatVoiceInfo(InstalledVoice iv)
    {
        var line = new StringBuilder(iv.VoiceInfo?.Name ?? "");
        line.Append(" (")
            .Append(iv.VoiceInfo?.Culture?.TwoLetterISOLanguageName.ToUpperInvariant() ?? "Unknown Language")
            .Append(")")
            .Append(" - ")
            .Append(iv.VoiceInfo?.Gender.ToString());

        if (iv.VoiceInfo?.Name?.Contains("Zira") ?? false)
        {
            line.Append(" [UNSTABLE]");
        }

        return line.ToString();
    }
}