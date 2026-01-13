using Dalamud.Bindings.ImGui;
using NAudio.CoreAudioApi;
using OpenAI;
using Serilog;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

    private readonly HttpClient _httpClient = new();

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

    public async Task Say(string text, string modelName, TextSource source, string voiceId, string? instructions, float speed, float volume)
    {
        long methodStart = Stopwatch.GetTimestamp();
        if (string.IsNullOrWhiteSpace(ApiKey)) return;

        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;

        try
        {
            // 1. Prepare the JSON Payload
            var requestBody = new Dictionary<string, object>
        {
            { "model", modelName },
            { "input", text },
            { "voice", voiceId.ToLowerInvariant() },
            { "response_format", "pcm" },
            { "speed", speed }
        };

            // Check if model supports instructions (gpt-4o-mini-tts)
            var modelCfg = Models.FirstOrDefault(m => m.ModelName == modelName);
            if (modelCfg != null && modelCfg.InstructionsSupported && !string.IsNullOrEmpty(instructions))
            {
                requestBody["instructions"] = instructions;
            }

            // 2. Configure the Request
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // 3. Send and Stream Response
            // HttpCompletionOption.ResponseHeadersRead is the "magic" for low latency
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync(token);

            // 4. Pass the live stream directly to the sound queue
            // The queue will handle the background reading/decoding
            _soundQueue.EnqueueSound(responseStream, source, volume, StreamFormat.Wave, null, methodStart);
        }
        catch (OperationCanceledException)
        {
            Log.Information("OpenAI Speech generation was cancelled.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenAI REST Speech generation failed.");
        }
    }
}
