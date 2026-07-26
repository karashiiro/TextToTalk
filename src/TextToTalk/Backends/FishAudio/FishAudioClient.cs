using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioClient
{
    private const string UrlBase = "https://api.fish.audio";

    private readonly HttpClient http;
    private readonly StreamSoundQueue soundQueue;

    public string? ApiKey { get; set; }

    public FishAudioClient(StreamSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;
    }

    public async Task Say(string? voiceId, int playbackRate, float volume, float temperature, float topP,
        string? model, string? latency, TextSource source, string text)
    {
        if (!IsAuthorizationSet())
        {
            throw new FishAudioMissingCredentialsException("No Fish Audio API key has been configured.");
        }

        var speed = playbackRate / 100.0f;
        var args = new FishAudioTtsRequest
        {
            Text = text,
            ReferenceId = voiceId,
            Format = "mp3",
            Latency = latency ?? "normal",
            Temperature = temperature,
            TopP = topP,
            Prosody = new FishAudioProsody { Speed = speed },
        };

        var uriBuilder = new UriBuilder(UrlBase) { Path = "/v1/tts" };
        using var req = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
        AddAuthorization(req);
        req.Headers.Add("model", model ?? "s2.1-pro-free");
        req.Headers.Add("accept", "audio/mpeg");

        DetailedLog.Verbose(JsonConvert.SerializeObject(args));
        using var content = new StringContent(JsonConvert.SerializeObject(args));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Content = content;

        var res = await this.http.SendAsync(req);
        EnsureSuccessStatusCode(res);

        var responseStream = await res.Content.ReadAsStreamAsync();
        var mp3Stream = new MemoryStream();
        await responseStream.CopyToAsync(mp3Stream);
        mp3Stream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(mp3Stream, source, StreamFormat.Mp3, volume);
    }

    public async Task<IList<FishAudioVoiceModel>> GetVoiceModels()
    {
        if (!IsAuthorizationSet())
        {
            throw new FishAudioMissingCredentialsException("No Fish Audio API key has been configured.");
        }

        var res = await SendRequest<FishAudioGetModelsResponse>("/model", "self=true&page_size=100");
        if (res?.Items == null)
        {
            throw new InvalidOperationException("Voice models endpoint returned null.");
        }

        return res.Items
            .Where(m => m.State == "trained")
            .ToList();
    }

    public async Task<FishAudioApiCreditInfo> GetApiCreditInfo()
    {
        if (!IsAuthorizationSet())
        {
            throw new FishAudioMissingCredentialsException("No Fish Audio API key has been configured.");
        }

        var res = await SendRequest<FishAudioApiCreditInfo>("/wallet/self/api-credit");
        if (res == null)
        {
            throw new InvalidOperationException("API credit endpoint returned null.");
        }

        return res;
    }

    private async Task<TResponse?> SendRequest<TResponse>(string path, string? query = null) where TResponse : class
    {
        var uriBuilder = new UriBuilder(UrlBase)
        {
            Path = path,
            Query = query ?? string.Empty,
        };

        using var req = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        AddAuthorization(req);
        req.Headers.Add("accept", "application/json");

        var res = await this.http.SendAsync(req);
        EnsureSuccessStatusCode(res);

        var resContent = await res.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TResponse>(resContent);
    }

    private static void EnsureSuccessStatusCode(HttpResponseMessage res)
    {
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new FishAudioUnauthorizedException(res.StatusCode, "Unauthorized request.");
        }

        if (!res.IsSuccessStatusCode)
        {
            throw new FishAudioFailedException(res.StatusCode, "Failed to make request.");
        }
    }

    private void AddAuthorization(HttpRequestMessage req)
    {
        req.Headers.Add("Authorization", $"Bearer {ApiKey}");
    }

    private bool IsAuthorizationSet()
    {
        return ApiKey is { Length: > 0 };
    }

    private class FishAudioTtsRequest
    {
        [JsonProperty("text")] public string? Text { get; init; }

        [JsonProperty("reference_id", NullValueHandling = NullValueHandling.Ignore)] public string? ReferenceId { get; init; }

        [JsonProperty("format")] public string? Format { get; init; }

        [JsonProperty("latency")] public string? Latency { get; init; }

        [JsonProperty("temperature")] public float Temperature { get; init; }

        [JsonProperty("top_p")] public float TopP { get; init; }

        [JsonProperty("prosody")] public FishAudioProsody? Prosody { get; init; }
    }

    private class FishAudioProsody
    {
        [JsonProperty("speed")] public float Speed { get; init; }
    }

    private class FishAudioGetModelsResponse
    {
        [JsonProperty("total")] public int Total { get; init; }

        [JsonProperty("items")] public FishAudioVoiceModel[]? Items { get; init; }
    }
}
