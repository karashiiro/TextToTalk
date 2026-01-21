using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Microsoft.ML.OnnxRuntime;
using PiperSharp;
using PiperSharp.Models;
using Serilog;
using System;
using System.Collections.Generic;
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
    private CancellationTokenSource cts = new();
    private readonly PluginConfiguration config;

    private Process? piperServerProcess;
    private readonly object processLock = new();

    public string GetVoicesDir(PluginConfiguration config) =>
        Path.Combine(config.GetPluginConfigDirectory(), "piper", "voices");

    public PiperBackend(PluginConfiguration config)
    {
        this.ui = new PiperBackendUI(config, this);

        // 1. Point to the nested 'piper' subfolder created by ExtractPiper
        string piperBaseDir = Path.Combine(config.GetPluginConfigDirectory(), "piper", "piper");
        string piperExe = Path.Combine(piperBaseDir, "piper.exe");

        this.piper = new PiperProvider(new PiperConfiguration()
        {
            ExecutableLocation = piperExe,
            WorkingDirectory = piperBaseDir
        });

        this.modelTask = LoadOrDownloadModelAsync(config);
        this.soundQueue = new StreamingSoundQueue(config);
        this.config = config;
    }

    public async Task<IDictionary<string, VoiceModel>> GetAvailableModels()
    {
        return await PiperDownloader.GetHuggingFaceModelList();
    }
    public static bool IsModelFileDownloaded(PluginConfiguration config)
    {
        var piperExePath = Path.Combine(config.GetPluginConfigDirectory(), "piper", "piper", "piper.exe");
        return File.Exists(piperExePath);
    }

    /// <summary>
    /// Downloads a specific model and initializes its folder structure.
    /// </summary>
    public async Task DownloadSpecificModel(string modelKey, VoiceModel entry)
    {
        string voicesDir = GetVoicesDir(config);
        string modelTargetDir = Path.Combine(voicesDir, modelKey);

        try
        {
            DetailedLog.Info($"Downloading voice: {modelKey}");
            await entry.DownloadModel(voicesDir);

            string onnxPath = Path.Combine(modelTargetDir, $"{modelKey}.onnx");
            await LoadSpecificVoiceModel(onnxPath);

            DetailedLog.Info($"Successfully installed {modelKey}");
        }
        catch (Exception ex)
        {
            DetailedLog.Error($"Failed to download {modelKey}: {ex.Message}");
        }
    }

    public bool DeleteVoiceModel(string modelKey)
    {
        try
        {
            // 1. Kill any active speech to unlock files
            KillActiveProcessInternal();

            string voicesDir = GetVoicesDir(config);
            string modelTargetDir = Path.Combine(voicesDir, modelKey);

            if (Directory.Exists(modelTargetDir))
            {
                Directory.Delete(modelTargetDir, true);
                DetailedLog.Info($"Deleted voice model: {modelKey}");
                return true;
            }
        }
        catch (Exception ex)
        {
            DetailedLog.Error($"Failed to delete {modelKey}: {ex.Message}");
        }
        return false;
    }

    /// Downloads the Piper executable and initial voice models.
    public async Task EnsurePiperAssetsDownloaded(PluginConfiguration config)
    {
        string configDir = config.GetPluginConfigDirectory();
        string voicesDir = GetVoicesDir(config);

        await EnsureExecutableDownloaded(config);

        var allModels = await PiperDownloader.GetHuggingFaceModelList();

        // TARGET: Only download "en_US-lessac-medium" initially
        string starterModelKey = "en_US-lessac-medium";

        if (allModels.TryGetValue(starterModelKey, out var modelEntry))
        {
            string modelTargetDir = Path.Combine(voicesDir, starterModelKey);

            // Skip if already downloaded
            if (!File.Exists(Path.Combine(modelTargetDir, $"{starterModelKey}.onnx")))
            {
                try
                {
                    DetailedLog.Info($"Downloading starter English voice: {starterModelKey}");

                    await modelEntry.DownloadModel(voicesDir);

                    string onnxPath = Path.Combine(modelTargetDir, $"{starterModelKey}.onnx");

                    await LoadSpecificVoiceModel(onnxPath);
                }
                catch (Exception ex)
                {
                    DetailedLog.Error($"Failed to download starter voice {starterModelKey}: {ex.Message}");
                }
            }
            else
            {
                DetailedLog.Info($"Starter voice {starterModelKey} already exists.");
            }
        }
        else
        {
            DetailedLog.Error($"Starter voice {starterModelKey} not found in the Hugging Face model list.");
        }
    }

    private async Task EnsureExecutableDownloaded(PluginConfiguration config)
    {
        if (!IsModelFileDownloaded(config))
        {
            string piperDir = Path.Combine(config.GetPluginConfigDirectory(), "piper");
            DetailedLog.Info("Piper executable missing. Downloading...");
            await PiperDownloader.DownloadPiper().ExtractPiper(piperDir);
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

        Task.Run(async () => await Say(request.Text, (PiperVoicePreset)request.Voice, request.Source));
    }

    public async Task Say(string text, PiperVoicePreset voicePreset, TextSource source)
    {
        long? timestampToPass = Stopwatch.GetTimestamp();

        // 1. Validation
        if (string.IsNullOrEmpty(voicePreset.ModelPath) || !File.Exists(voicePreset.ModelPath))
        {
            DetailedLog.Error($"Piper model file not found: {voicePreset.ModelPath}");
            return;
        }

        try
        {
            // 2. Prepare Model and Arguments
            var voiceDir = Path.GetDirectoryName(voicePreset.ModelPath);
            piper.Configuration.Model = await VoiceModel.LoadModel(voiceDir);
            piper.Configuration.SpeakingRate = 1.0f / (voicePreset.Speed ?? 1f);

            string args = piper.Configuration.BuildArguments();

            // 3. Initialize Process
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = piper.Configuration.ExecutableLocation,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
             
            };

            // 4. Thread-Safe Process Management
            lock (processLock)
            {
                // Kill any dangling process before starting a new one
                KillActiveProcessInternal();
                piperServerProcess = process;
            }

            // 5. THE CANCELLATION BRIDGE
            using var registration = cts.Token.Register(() => KillActiveProcessInternal());

            process.Start();

            // 6. Check for cancellation before writing to the pipe
            if (cts.Token.IsCancellationRequested)
                throw new OperationCanceledException(cts.Token);

            // 7. Write Text to StandardInput
            using (var sw = new StreamWriter(process.StandardInput.BaseStream, leaveOpen: false))
            {
                await sw.WriteLineAsync(text);
                await sw.FlushAsync();
            }

            // 8. Determine Audio Format
            var format = voicePreset.InternalName switch
            {
                string name when name.EndsWith("low") => StreamFormat.PiperLow,
                string name when name.EndsWith("high") => StreamFormat.PiperHigh,
                _ => StreamFormat.Piper // Defaults to Medium/Standard
            };

            // 9. Enqueue Stream
            soundQueue.EnqueueSound(process.StandardOutput.BaseStream, source, voicePreset.Volume ?? 1f, format, null, timestampToPass);

            // 10. Await process exit
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Piper synthesis task was cancelled.");
        }
        catch (Exception ex)
        {
            DetailedLog.Error($"Piper streaming failed: {ex.Message}");
        }
        finally
        {
            KillActiveProcessInternal();
        }
    }

    public void KillActiveProcessInternal()
    {
        lock (processLock)
        {
            if (piperServerProcess != null)
            {
                try
                {
                    piperServerProcess.Kill(true);
                }
                catch (Exception ex)
                {
                    DetailedLog.Debug($"Error killing piper process: {ex.Message}");
                }
                finally
                {
                    piperServerProcess.Dispose();
                    piperServerProcess = null;
                }
            }
        }
    }

    public override void CancelAllSpeech()
    {
        KillActiveProcessInternal();

        soundQueue.CancelAllSounds();
        soundQueue.StopHardware();
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
    }

    public override void CancelSay(TextSource source)
    {
        KillActiveProcessInternal();

        soundQueue.CancelFromSource(source);
        soundQueue.StopHardware();
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
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