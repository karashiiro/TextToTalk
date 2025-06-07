using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using ImGuiNET;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;

namespace TextToTalk.Backends.Kokoro;

public class KokoroBackend : VoiceBackend
{
    private const string ModelUri = "https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/kokoro-quant.onnx";
    private readonly KokoroSoundQueue soundQueue;
    private readonly KokoroBackendUI ui;
    private readonly Task<KokoroModel> modelTask;
    private readonly CancellationTokenSource cts = new();

    public KokoroBackend(PluginConfiguration config)
    {
        ui = new KokoroBackendUI(config, this);

        Tokenizer.eSpeakNGPath = Path.Join(config.GetPluginAssemblyDirectory(), "espeak");

        modelTask = GetModelAsync(config);
        soundQueue = new KokoroSoundQueue(config, modelTask);

        KokoroVoiceManager.LoadVoicesFromPath(Path.Join(config.GetPluginAssemblyDirectory(), "voices"));
        DetailedLog.Info($"Kokoro voices loaded: {KokoroVoiceManager.Voices.Count} voices available.");
    }

    private bool TryGetModel([NotNullWhen(true)] out KokoroModel? tts)
    {
        if (modelTask.IsCompletedSuccessfully)
        {
            tts = modelTask.Result;
            return true;
        }
        tts = null;
        return false;
    }

    private async Task<KokoroModel> GetModelAsync(PluginConfiguration config)
    {
        var path = Path.Join(config.GetPluginConfigDirectory(), "kokoro-quant.onnx");
        DetailedLog.Debug($"Checking for Kokoro model at '{path}'.");
        if (Path.Exists(path))
        {
            // check file hash to verify integrity
            var hash = SHA256.HashData(await File.ReadAllBytesAsync(path, cts.Token));
            if (Convert.ToHexString(hash) == "C1610A859F3BDEA01107E73E50100685AF38FFF88F5CD8E5C56DF109EC880204")
            {
                DetailedLog.Debug($"Kokoro model file at '{path}' is valid. Using existing model.");
                return new KokoroModel(path);
            }
            else
            {
                DetailedLog.Warn($"Kokoro model file at '{path}' has an invalid hash. Re-downloading the model.");
                File.Delete(path);
            }
        }

        DetailedLog.Debug($"Downloading Kokoro model from '{ModelUri}' to '{path}'.");
        using var client = new HttpClient();
        using var response = await client.GetAsync(ModelUri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
        using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await responseStream.CopyToAsync(fileStream, cts.Token);
            await fileStream.FlushAsync(cts.Token);
        }
        DetailedLog.Debug($"Kokoro model downloaded successfully.");
        return new KokoroModel(path);
    }

    public override void Say(SayRequest request)
    {
        if (request.Voice is not KokoroVoicePreset voicePreset)
            throw new InvalidOperationException("Invalid voice preset provided.");
        Say(request.Text, voicePreset, request.Source, request.Language);
    }

    public void Say(string text, KokoroVoicePreset voicePreset, TextSource source, ClientLanguage language)
    {
        if (!TryGetModel(out _))
        {
            return;
        }
        var voice = KokoroVoiceManager.GetVoice(voicePreset.InternalName);

        DetailedLog.Debug($"Saying text with voice: {voicePreset.InternalName}:\n{text}");

        if (source == TextSource.Chat)
        {
            // Chat messages are not dependent on the client setting, so we default to English
            language = ClientLanguage.English;
        }

        // TODO: apply lexicon once KokoroSharp supports it
        soundQueue.EnqueueSound(new(text, voice, voicePreset.Speed ?? 1f, source, language));
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
            ImGui.TextColored(BackendUI.Red, $"Failed to download model: {modelTask.Exception?.Message}");
            DetailedLog.Error($"Failed to download Kokoro model: {modelTask.Exception}");
        }
        else
        {
            ImGui.TextColored(BackendUI.HintColor, $"Model is still downloading...");
        }
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        return soundQueue.GetCurrentlySpokenTextSource();
    }

    protected override void Dispose(bool disposing)
    {
        cts.Cancel();
        if (disposing)
        {
            if (TryGetModel(out var model))
            {
                model.Dispose();
            }
            soundQueue.Dispose();
        }
    }
}