using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiClient(StreamSoundQueue soundQueue, HttpClient http)
{
    private const string UrlBase = "https://api.openai.com";

    public static readonly IReadOnlySet<string> Models = new HashSet<string>
    {
        "tts-1",
        "tts-1-hd",
    };

    public static readonly IReadOnlySet<string> Voices = new HashSet<string>
    {
        "alloy",
        "echo",
        "fable",
        "onyx",
        "nova",
        "shimmer",
    };

    public string? ApiKey { get; set; }

    private void AddAuthorization(HttpRequestMessage req)
    {
        req.Headers.Add("Authorization", $"Bearer {ApiKey}");
    }

    private bool IsAuthorizationSet()
    {
        return ApiKey is { Length: > 0 };
    }

    public async Task TestCredentials()
    {
        if (!IsAuthorizationSet())
        {
            throw new OpenAiMissingCredentialsException("No OpenAI authorization keys have been configured.");
        }

        var uriBuilder = new UriBuilder(UrlBase) { Path = "/v1/models" };
        using var req = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        AddAuthorization(req);

        var res = await http.SendAsync(req);
        await EnsureSuccessStatusCode(res);
    }

    public async Task Say(string? voice, float? speed, float volume, TextSource source, string text)
    {
        if (!IsAuthorizationSet())
        {
            throw new OpenAiMissingCredentialsException("No OpenAI authorization keys have been configured.");
        }

        var uriBuilder = new UriBuilder(UrlBase) { Path = "/v1/audio/speech" };
        using var req = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
        AddAuthorization(req);

        var args = new
        {
            model = "tts-1",
            input = text,
            voice = voice?.ToLower() ?? "alloy",
            response_format = "mp3",
            speed = speed ?? 1.0f,
        };

        var json = JsonSerializer.Serialize(args);
        DetailedLog.Verbose(json);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Content = content;

        var res = await http.SendAsync(req);
        await EnsureSuccessStatusCode(res);

        var mp3Stream = new MemoryStream();
        var responseStream = await res.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(mp3Stream);
        mp3Stream.Seek(0, SeekOrigin.Begin);

        soundQueue.EnqueueSound(mp3Stream, source, StreamFormat.Mp3, volume);
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage res)
    {
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new OpenAiUnauthorizedException(res.StatusCode, "Unauthorized request.");
        }

        if (!res.IsSuccessStatusCode)
        {
            try
            {
                var content = await res.Content.ReadAsStringAsync();
                DetailedLog.Debug(content);

                var error = JsonSerializer.Deserialize<OpenAiErrorResponse>(content);
                if (error?.Error != null)
                {
                    throw new OpenAiFailedException(res.StatusCode, error.Error,
                        $"Request failed with status code {error.Error.Code}: {error.Error.Message}");
                }
            }
            catch (Exception e) when (e is not OpenAiFailedException)
            {
                DetailedLog.Error(e, "Failed to parse OpenAI error response.");
            }

            throw new OpenAiFailedException(res.StatusCode, null, $"Request failed with status code {res.StatusCode}.");
        }
    }
}