using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiClient
{
    private const string UrlBase = "https://api.openai.com";

    public static readonly IReadOnlySet<string> Models = new HashSet<string>
    {
        "tts-1",
        "tts-1-hd"
    };

    public static readonly IReadOnlySet<string> Voices = new HashSet<string>
    {
        "alloy",
        "echo",
        "fable",
        "onyx",
        "nova",
        "shimmer"
    };

    private readonly HttpClient client;
    private readonly StreamSoundQueue soundQueue;

    public OpenAiClient(StreamSoundQueue soundQueue, HttpClient http)
    {
        this.soundQueue = soundQueue;
        client = http;
    }

    public string? ApiKey { get; set; }

    private void AddAuthorization(HttpRequestMessage req)
    {
        req.Headers.Add("Authorization", $"Bearer {ApiKey}");
    }

    public async Task Say(string? voice, float? speed, float volume, TextSource source, string text)
    {
        var uriBuilder = new UriBuilder(UrlBase) {Path = "/v1/audio/speech"};
        using var req = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
        AddAuthorization(req);

        var args = new
        {
            model = "tts-1",
            input = text,
            voice = voice?.ToLower() ?? "alloy",
            response_format = "mp3",
            speed = speed ?? 1.0f
        };

        var json = JsonSerializer.Serialize(args);
        DetailedLog.Info(json);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Content = content;

        var res = await client.SendAsync(req);
        EnsureSuccessStatusCode(res);

        var mp3Stream = new MemoryStream();
        var responseStream = await res.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(mp3Stream);
        mp3Stream.Seek(0, SeekOrigin.Begin);

        soundQueue.EnqueueSound(mp3Stream, source, StreamFormat.Mp3, volume);
    }

    private static void EnsureSuccessStatusCode(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Request failed with status code {res.StatusCode}.");
    }
}