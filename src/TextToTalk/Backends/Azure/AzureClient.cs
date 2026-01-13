using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;

    private readonly SpeechConfig speechConfig;
    private readonly SpeechSynthesizer synthesizer;

    private readonly StreamingSoundQueue soundQueue;
    private readonly LexiconManager _lexiconManager;
    private readonly PluginConfiguration config;
    private CancellationTokenSource? _ttsCts;

    public AzureClient(string subscriptionKey, string region, LexiconManager lexiconManager, PluginConfiguration config)
    {
        _apiKey = subscriptionKey;
        _endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TextToTalkApp");

        soundQueue = new StreamingSoundQueue(config);
        _lexiconManager = lexiconManager;
        speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);
        synthesizer = new SpeechSynthesizer(speechConfig, null);
    }

    public TextSource GetCurrentlySpokenTextSource()
    {
        return this.soundQueue.GetCurrentlySpokenTextSource();
    }
    public List<VoiceDetails> GetVoicesWithStyles()
    {
        // Fetches the voice result asynchronously and waits for completion
        var res = this.synthesizer.GetVoicesAsync().GetAwaiter().GetResult();
        HandleResult(res);

        // Maps each voice to a custom object containing Name and StyleList
        return res.Voices.Select(voice => new VoiceDetails
        {
            Name = voice.Name,
            ShortName = voice.ShortName,
            Styles = voice.StyleList.ToList() // StyleList is a string[]
        }).ToList();
    }

    public class VoiceDetails
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public List<string> Styles { get; set; }
    }

    public List<string> GetVoices()
    {
        var res = this.synthesizer.GetVoicesAsync().GetAwaiter().GetResult();
        HandleResult(res);
        return res.Voices.Select(voice => voice.Name).ToList();
    }

    public async Task Say(string? voice, int playbackRate, float volume, TextSource source, string text, string style)
    {
        long methodStart = Stopwatch.GetTimestamp();
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;

        
        var ssml = _lexiconManager.MakeSsml(text, style, voice, "en-US", playbackRate, true);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(ssml, global::System.Text.Encoding.UTF8, "application/ssml+xml")
        };

        // 2026 Low Latency Format: 'raw' is better for direct streaming than 'riff'
        request.Headers.Add("X-Microsoft-OutputFormat", "raw-16khz-16bit-mono-pcm");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync(token);

        byte[] buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
        {
            if (token.IsCancellationRequested) break;

            var chunk = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

            var chunkStream = new MemoryStream(chunk);
            long? timestampToPass = methodStart;
            soundQueue.EnqueueSound(chunkStream, source, volume, StreamFormat.Azure, null, timestampToPass);

        }

        // Implicitly returns Task.CompletedTask because it is 'async Task'
    }


    public Task CancelAllSounds()
    {
        //this.synthesizer.Dispose();
        this.soundQueue.CancelAllSounds();
        this.soundQueue.StopHardware();
        this.soundQueue.CancelAllSounds();
        return Task.CompletedTask;
    }

    public Task CancelFromSource(TextSource source)
    {
        this.synthesizer.StopSpeakingAsync();
        this.soundQueue.StopHardware();
        this.soundQueue.CancelFromSource(source);
        return Task.CompletedTask;
    }

    private static void HandleResult(SynthesisVoicesResult res)
    {
        if (!string.IsNullOrEmpty(res.ErrorDetails))
        {
            DetailedLog.Error($"Azure request error: ({res.Reason}) \"{res.ErrorDetails}\"");
        }
    }

    private static void HandleResult(SpeechSynthesisResult res)
    {
        if (res.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(res);
            if (cancellation.Reason == CancellationReason.Error)
            {
                DetailedLog.Error($"Azure request error: ({cancellation.ErrorCode}) \"{cancellation.ErrorDetails}\"");
            }
            else
            {
                DetailedLog.Warn($"Azure request failed in state \"{cancellation.Reason}\"");
            }

            return;
        }

        if (res.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            DetailedLog.Warn($"Speech synthesis request completed in incomplete state \"{res.Reason}\"");
        }
    }

    public void Dispose()
    {
        this.synthesizer?.Dispose();
        this.soundQueue?.Dispose();
        this.soundQueue?.Dispose();
    }
}