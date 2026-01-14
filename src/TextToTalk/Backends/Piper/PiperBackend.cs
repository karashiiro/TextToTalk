using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using PiperSharp;
using PiperSharp.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Backends.Kokoro;
using TextToTalk.Backends.Piper;

namespace TextToTalk.Backends.Piper;

public class PiperBackend : VoiceBackend
{
    private readonly PiperProvider piper;
    private readonly PiperBackendUI ui;
    private readonly StreamingSoundQueue soundQueue;
    private readonly Task<VoiceModel> modelTask;
    private readonly CancellationTokenSource cts = new();

    private Process? piperServerProcess;

    private string GetVoicesDir(PluginConfiguration config) =>
        Path.Combine(config.GetPluginConfigDirectory(), "piper", "voices");

    public PiperBackend(PluginConfiguration config)
    {
        ui = new PiperBackendUI(config, this);
        string piperExe = Path.Join(config.GetPluginConfigDirectory(), "piper", "piper.exe");

        piper = new PiperProvider(new PiperConfiguration()
        {
            ExecutableLocation = piperExe,
            WorkingDirectory = Path.GetDirectoryName(piperExe)
        });

        modelTask = LoadOrDownloadModelAsync(config);
        soundQueue = new StreamingSoundQueue(config);
    }
    public static bool IsModelFileDownloaded(PluginConfiguration config)
    {
        var piperExePath = Path.Combine(config.GetPluginConfigDirectory(), "piper", "piper.exe");
        return File.Exists(piperExePath);
    }

    /// Downloads the Piper executable and initial voice models.
    public async Task EnsurePiperAssetsDownloaded(PluginConfiguration config)
    {
        string configDir = config.GetPluginConfigDirectory();
        string voicesDir = GetVoicesDir(config);
        var allModels = await PiperDownloader.GetHuggingFaceModelList();

        var filteredModels = allModels
            .Where(m => m.Key.StartsWith("en") && m.Key.EndsWith("medium"))
            .ToList();

        foreach (var modelEntry in filteredModels)
        {
            string modelKey = modelEntry.Key;
            string modelTargetDir = Path.Combine(voicesDir, modelKey);

            if (File.Exists(Path.Combine(modelTargetDir, $"{modelKey}.onnx"))) continue;

            try
            {
                DetailedLog.Info($"Downloading English medium voice: {modelKey}");
                await modelEntry.Value.DownloadModel(voicesDir);

                string onnxPath = Path.Combine(modelTargetDir, $"{modelKey}.onnx");
                await LoadSpecificVoiceModel(onnxPath);
            }
            catch (Exception ex)
            {
                DetailedLog.Error($"Failed to download {modelKey}: {ex.Message}");
            }
        }
    }

    private async Task<VoiceModel> LoadOrDownloadModelAsync(PluginConfiguration config)
    {

        string modelKey = "en_US-lessac-medium";
        string modelDir = Path.Combine(GetVoicesDir(config), modelKey);
        string onnxFilePath = Path.Combine(modelDir, $"{modelKey}.onnx");

        if (!File.Exists(onnxFilePath))
        {
            await EnsurePiperAssetsDownloaded(config);
        }

        return await LoadSpecificVoiceModel(onnxFilePath);
    }

    private async Task<VoiceModel> LoadSpecificVoiceModel(string onnxFilePath)
    {
        string modelDir = Path.GetDirectoryName(onnxFilePath);
        string configFilePath = onnxFilePath + ".json";
        string piperSharpExpectedJson = Path.Combine(modelDir, "model.json");

        if (File.Exists(configFilePath) && !File.Exists(piperSharpExpectedJson))
        {
            File.Copy(configFilePath, piperSharpExpectedJson, true);
        }

        return await VoiceModel.LoadModel(modelDir);
    }

    private bool TryGetModel([NotNullWhen(true)] out VoiceModel? tts)
    {
        if (modelTask.IsCompletedSuccessfully)
        {
            tts = modelTask.Result;
            return true;
        }

        tts = null;
        return false;
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not PiperVoicePreset voicePreset)
            throw new InvalidOperationException("Invalid voice preset.");

        if (!modelTask.IsCompletedSuccessfully) return;

        Say(request.Text, voicePreset, request.Source);
    }

    public async Task Say(string text, PiperVoicePreset voicePreset, TextSource source)
    {
        long methodStart = Stopwatch.GetTimestamp();
        long? timestampToPass = methodStart;

        if (string.IsNullOrEmpty(voicePreset.ModelPath) || !File.Exists(voicePreset.ModelPath))
        {
            DetailedLog.Error($"Piper model file not found: {voicePreset.ModelPath}");
            return;
        }

        try
        {
            var voiceDir = Path.GetDirectoryName(voicePreset.ModelPath);
            var voiceModel = await VoiceModel.LoadModel(voiceDir);

            piper.Configuration.Model = voiceModel;
            piper.Configuration.SpeakingRate = 1.0f / voicePreset.Speed ?? 1f;

            byte[] audioData = await piper.InferAsync(text, AudioOutputType.Raw, cts.Token);
            if (audioData == null || audioData.Length == 0) return;
            var audioStream = new MemoryStream(audioData);
            soundQueue.EnqueueSound(audioStream, source, voicePreset.Volume ?? 1f, StreamFormat.Piper, null, timestampToPass);
        }
        catch (Exception ex)
        {
            DetailedLog.Error($"Piper switching/inference failed: {ex.Message}");
        }
    }

    public override void CancelAllSpeech()
    {
        soundQueue.CancelAllSounds();
    }

    public override void CancelSay(TextSource source)
    {
        soundQueue.CancelFromSource(source);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        if (TryGetModel(out _))
        {
            ui.DrawVoicePresetOptions();
            return;
        }

        if (modelTask.Status == TaskStatus.Faulted)
        {
            ImGui.TextColored(ImColor.Red, $"Failed to download model: {modelTask.Exception?.Message}");
            DetailedLog.Error($"Failed to download Piper model: {modelTask.Exception}");
        }
        else
        {
            ImGui.TextColored(ImColor.HintColor, "Model is still downloading or initializing...");
        }
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return soundQueue.GetCurrentlySpokenTextSource();
    }
    public override void DrawStyles(IConfigUIDelegates helpers)
    {
        helpers.OpenVoiceStylesConfig();
    }
    protected override void Dispose(bool disposing)
    {
        cts.Cancel();

        if (disposing)
        {
            soundQueue?.Dispose();
            cts.Dispose();
        }
    }

}