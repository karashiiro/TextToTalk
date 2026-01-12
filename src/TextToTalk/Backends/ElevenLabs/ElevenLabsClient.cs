using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsClient
{
    private const string UrlBase = "https://api.elevenlabs.io";

    private readonly HttpClient http;
    private readonly StreamingSoundQueue soundQueue;

    public string? ApiKey { get; set; }

    public CancellationTokenSource? _TtsCts;

    public ElevenLabsClient(StreamingSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;
    }

    public async Task Say(string? voice, int playbackRate, float volume, float similarityBoost, float stability,
        TextSource source, string text, string? model, string? style)
    {
        Log.Information($"Style = {style}");
        _TtsCts?.Cancel();
        _TtsCts?.Dispose();

        _TtsCts = new CancellationTokenSource();
        var ct = _TtsCts.Token;

        try
        {
            if (!IsAuthorizationSet())
            {
                throw new ElevenLabsMissingCredentialsException("No ElevenLabs authorization keys have been configured.");
            }

            if (!string.IsNullOrEmpty(style))
            {
                model = "eleven_v3";
                text = $"[{style}] " + text;
            }

            float finalStability = stability;
            if (model == "eleven_v3")
            {
                finalStability = (float)Math.Round(stability * 2.0f, MidpointRounding.AwayFromZero) / 2.0f;
            }

            var args = new ElevenLabsTextToSpeechRequest
            {
                Text = text,
                ModelId = model,
                VoiceSettings = new ElevenLabsVoiceSettings
                {
                    SimilarityBoost = similarityBoost,
                    Stability = finalStability,
                },
            };
            Log.Information($"Model Called = {args.ModelId}");
            Log.Information($"Message Sent = {args.Text}");

            var uriBuilder = new UriBuilder(UrlBase) { Path = $"/v1/text-to-speech/{voice}/stream" };

            // Use HttpCompletionOption.ResponseHeadersRead to begin processing before the body is fully downloaded
            using var req = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
            AddAuthorization(req);
            req.Headers.Add("accept", "audio/mpeg");

            using var content = new StringContent(JsonConvert.SerializeObject(args), Encoding.UTF8, "application/json");
            req.Content = content;

            // SendAsync with ResponseHeadersRead is the key for streaming
            var res = await this.http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            EnsureSuccessStatusCode(res);

            // Get the stream directly from the response
            var responseStream = await res.Content.ReadAsStreamAsync(ct);

            // Enqueue the live stream. 
            // IMPORTANT: Your soundQueue must be able to process the stream as bytes arrive.
            this.soundQueue.EnqueueSound(responseStream, source, volume, StreamFormat.Mp3, res);
        }
        catch (OperationCanceledException)
        {
            // 2026 Best Practice: Catch the cancellation exception to prevent it 
            // from bubbling up as a generic error.
            Log.Information("TTS generation was cancelled.");
        }
    }


    public async Task<ElevenLabsUserSubscriptionInfo> GetUserSubscriptionInfo()
    {
        if (!IsAuthorizationSet())
        {
            throw new ElevenLabsMissingCredentialsException("No ElevenLabs authorization keys have been configured.");
        }

        var res = await SendRequest<ElevenLabsUserSubscriptionInfo>("/v1/user/subscription");
        if (res == null)
        {
            throw new InvalidOperationException("User subscription info endpoint returned null.");
        }

        return res;
    }

    public async Task<IDictionary<string, IList<ElevenLabsVoice>>> GetVoices()
    {
        if (!IsAuthorizationSet())
        {
            throw new ElevenLabsMissingCredentialsException("No ElevenLabs authorization keys have been configured.");
        }

        var res = await SendRequest<ElevenLabsGetVoicesResponse>("/v1/voices");
        if (res?.Voices == null)
        {
            throw new InvalidOperationException("Voices endpoint returned null.");
        }

        return res.Voices
            .GroupBy(v => v.Category ?? "")
            .ToImmutableSortedDictionary(
                g => g.Key,
                g => (IList<ElevenLabsVoice>)g.OrderByDescending(v => v.Name).ToList());
    }

    public async Task<IList<ElevenLabsModel>> GetModels()
    {
        if (!IsAuthorizationSet())
        {
            throw new ElevenLabsMissingCredentialsException("No ElevenLabs authorization keys have been configured.");
        }
        var res = await SendRequest<List<ElevenLabsModel>>("/v1/models");
        if (res == null)
        {
            throw new InvalidOperationException("Models endpoint returned null.");
        }
        return res.OrderByDescending(v => v.ModelId).ToList();
    }


    private async Task<TResponse?> SendRequest<TResponse>(string endpoint, string query = "",
        HttpContent? reqContent = null) where TResponse : class
    {
        var uriBuilder = new UriBuilder(UrlBase)
        {
            Path = endpoint,
            Query = query,
        };

        using var req = new HttpRequestMessage(reqContent != null ? HttpMethod.Post : HttpMethod.Get, uriBuilder.Uri);
        AddAuthorization(req);
        req.Headers.Add("accept", "application/json");

        if (reqContent != null)
        {
            req.Content = reqContent;
        }

        var res = await this.http.SendAsync(req);
        EnsureSuccessStatusCode(res);

        var resContent = await res.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TResponse>(resContent);
    }

    private static void EnsureSuccessStatusCode(HttpResponseMessage res)
    {
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ElevenLabsUnauthorizedException(res.StatusCode, "Unauthorized request.");
        }

        if (!res.IsSuccessStatusCode)
        {
            throw new ElevenLabsFailedException(res.StatusCode, "Failed to make request.");
        }
    }

    private void AddAuthorization(HttpRequestMessage req)
    {
        req.Headers.Add("xi-api-key", ApiKey);
    }

    private bool IsAuthorizationSet()
    {
        return ApiKey is { Length: > 0 };
    }

    private class ElevenLabsGetVoicesResponse
    {
        [JsonProperty("voices")] public ElevenLabsVoice[]? Voices { get; init; }
    }

    private class ElevenLabsTextToSpeechRequest
    {
        [JsonProperty("text")] public string? Text { get; init; }

        [JsonProperty("model_id")] public string? ModelId { get; init; }

        [JsonProperty("voice_settings")] public ElevenLabsVoiceSettings? VoiceSettings { get; init; }
    }

    private class ElevenLabsVoiceSettings
    {
        [JsonProperty("similarity_boost")] public float SimilarityBoost { get; init; }

        [JsonProperty("stability")] public float Stability { get; init; }
    }
}