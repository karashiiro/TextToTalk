using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TextToTalk.Backends.ElevenLabs;

public class ElevenLabsClient
{
    private const string UrlBase = "https://api.elevenlabs.io";

    private readonly HttpClient http;
    private readonly StreamSoundQueue soundQueue;

    public string? ApiKey { get; set; }

    public ElevenLabsClient(StreamSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;
    }

    public async Task Say(string? voice, int playbackRate, float volume, TextSource source, string text)
    {
        if (!IsAuthorizationSet())
        {
            throw new ElevenLabsMissingCredentialsException("No ElevenLabs authorization keys have been configured.");
        }

        var args = new ElevenLabsTextToSpeechRequest
        {
            Text = text,
            ModelId = "eleven_monolingual_v1",
        };

        // Make the request
        var uriBuilder = new UriBuilder(UrlBase) { Path = $"/v1/text-to-speech/{voice}/stream" };
        using var req = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
        AddAuthorization(req);
        req.Headers.Add("accept", "audio/mpeg");

        using var content = new StringContent(JsonConvert.SerializeObject(args));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Content = content;

        var res = await this.http.SendAsync(req);
        EnsureSuccessStatusCode(res);

        // Copy the sound to a new buffer and enqueue it
        var responseStream = await res.Content.ReadAsStreamAsync();
        var mp3Stream = new MemoryStream();
        await responseStream.CopyToAsync(mp3Stream);
        mp3Stream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(mp3Stream, source, StreamFormat.Mp3, volume);
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
    }
}