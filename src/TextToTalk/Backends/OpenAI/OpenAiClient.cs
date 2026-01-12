using NAudio.CoreAudioApi;
using OpenAI;
using Serilog;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAIAudio = OpenAI.Audio;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiClient
{
    private readonly OpenAIClient _openAiClient;
    private readonly StreamingSoundQueue _soundQueue;
    public CancellationTokenSource? _ttsCts;

    // --- Provided Definitions ---
    public record ModelConfig(
        string ModelName,
        IReadOnlyDictionary<string, string> Voices,
        bool InstructionsSupported,
        bool SpeedSupported);

    private static readonly Dictionary<string, string> VoiceLabels = new()
    {
        { "alloy", "Alloy (Neutral & Balanced)" },
        { "ash", "Ash (Clear & Precise)" },
        { "ballad", "Ballad (Melodic & Smooth)" },
        { "coral", "Coral (Warm & Friendly)" },
        { "echo", "Echo (Resonant & Deep)" },
        { "fable", "Fable (Alto Narrative)" },
        { "onyx", "Onyx (Deep & Energetic)" },
        { "nova", "Nova (Bright & Energetic)" },
        { "sage", "Sage (Calm & Thoughtful)" },
        { "shimmer", "Shimmer (Bright & Feminine)" },
        { "verse", "Verse (Versatile & Expressive)" },
        { "marin", "Marin (Latest and Greatest)" },
        { "cedar", "Cedar (Latest and Greatest)" }
    };

    public static readonly List<ModelConfig> Models =
    [
        new("gpt-4o-mini-tts", VoiceLabels.ToDictionary(v => v.Key, v => v.Value), true, true),
        new("tts-1", VoiceLabels.Where(v => v.Key != "ballad" && v.Key != "verse").ToDictionary(v => v.Key, v => v.Value), false, true),
        new("tts-1-hd", VoiceLabels.Where(v => v.Key != "ballad" && v.Key != "verse").ToDictionary(v => v.Key, v => v.Value), false, true)
    ];

    public string? ApiKey { get; set; }

    // --- Implementation ---
    public OpenAiClient(StreamingSoundQueue soundQueue, string apiKey)
    {
        _soundQueue = soundQueue;
        ApiKey = apiKey;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _openAiClient = new OpenAIClient(apiKey);
        }
    }

    public async Task Say(string text, string modelName, string voiceId, string? instructions, float speed, float volume)
    {
        if (_openAiClient == null) return;

        // Cancel any previous request before starting a new one
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;

        try
        {
            OpenAIAudio.AudioClient audioClient = _openAiClient.GetAudioClient(modelName);

            var requestBody = new Dictionary<string, object>
        {
            { "model", modelName },
            { "input", text },
            { "voice", voiceId.ToLowerInvariant() },
            { "response_format", "mp3" },
            { "speed", speed }
        };

            if (Models.First(m => m.ModelName == "gpt-4o-mini-tts").InstructionsSupported)
            {
                requestBody["instructions"] = instructions ?? "";
            }

            BinaryContent content = BinaryContent.Create(BinaryData.FromObjectAsJson(requestBody));
            RequestOptions options = new();
            options.BufferResponse = false;

            // PASS THE TOKEN HERE
            options.CancellationToken = token;

            // The request will throw OperationCanceledException if cancelled during the call
            ClientResult result = await audioClient.GenerateSpeechAsync(content, options);

            Stream liveAudioStream = result.GetRawResponse().ContentStream;

            // Register a callback to close the stream if cancellation happens while reading
            token.Register(() => liveAudioStream.Close());

            Log.Information("Queuing Sound");
            _soundQueue.EnqueueSound(liveAudioStream, TextSource.None, volume, StreamFormat.Mp3, null);
        }
        catch (OperationCanceledException)
        {
            Log.Information("OpenAI Speech generation was cancelled by the user.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenAI Streaming Speech generation failed.");
        }
    }
}
