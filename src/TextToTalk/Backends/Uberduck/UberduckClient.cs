using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Logging;

namespace TextToTalk.Backends.Uberduck;

public class UberduckClient
{
    private const string UrlBase = "https://api.uberduck.ai";

    private readonly HttpClient http;
    private readonly StreamSoundQueue soundQueue;

    public string ApiKey { private get; set; }
    public string ApiSecret { private get; set; }

    public UberduckClient(StreamSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;
    }

    public async Task<IDictionary<string, IList<UberduckVoice>>> GetVoices()
    {
        var voicesRes = await this.http.GetStringAsync(new Uri("https://api.uberduck.ai/voices?mode=tts-basic"));
        var voices = JsonConvert.DeserializeObject<IList<UberduckVoice>>(voicesRes);
        return voices
            .GroupBy(v => v.Category)
            .ToImmutableSortedDictionary(
                g => g.Key,
                g => (IList<UberduckVoice>)g.OrderByDescending(v => v.DisplayName).ToList());
    }

    public async Task Say(string voice, int playbackRate, float volume, TextSource source, string text)
    {
        if (!IsAuthorizationSet())
        {
            throw new UberduckMissingCredentialsException("No Uberduck authorization keys have been configured.");
        }

        var args = new UberduckSpeechRequest
        {
            Speech = text,
            Voice = voice,
        };

        // Make the request
        using var content = new StringContent(JsonConvert.SerializeObject(args));
        var res = await SendRequest<UberduckSpeechResponse>("/speak", reqContent: content);
        var uuid = res.Uuid;
        PluginLog.LogDebug($"Got request UUID {uuid} from Uberduck.");

        // Poll for the TTS result
        await Task.Delay(20);

        var path = "";
        do
        {
            try
            {
                var status = await GetSpeechStatus(uuid);
                if (status.FailedAt != null)
                {
                    PluginLog.LogWarning($"TTS request {uuid} failed for an unknown reason.");
                    return;
                }

                path = status.Path;
            }
            catch (UberduckFailedException e) when (e.StatusCode is HttpStatusCode.NotFound)
            {
                // ignored
            }

            await Task.Delay(100);
        } while (string.IsNullOrEmpty(path));

        PluginLog.LogDebug($"Got response for TTS request {uuid}.");

        // Copy the sound to a new buffer and enqueue it
        var responseStream = await this.http.GetStreamAsync(new Uri(path));
        var waveStream = new MemoryStream();
        await responseStream.CopyToAsync(waveStream);
        waveStream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(waveStream, source, StreamFormat.Wave, volume);
    }

    private Task<UberduckSpeechStatusResponse> GetSpeechStatus(string uuid)
    {
        return SendRequest<UberduckSpeechStatusResponse>("/speak-status", $"uuid={uuid}");
    }

    private async Task<TResponse> SendRequest<TResponse>(string endpoint, string query = "",
        HttpContent reqContent = null) where TResponse : class
    {
        var uriBuilder = new UriBuilder(UrlBase)
        {
            Path = endpoint,
            Query = query,
        };

        using var req = new HttpRequestMessage(reqContent != null ? HttpMethod.Post : HttpMethod.Get, uriBuilder.Uri);
        AddAuthorization(req);

        if (reqContent != null)
        {
            req.Content = reqContent;
        }

        var res = await this.http.SendAsync(req);
        var resContent = await res.Content.ReadAsStringAsync();
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var detail = JsonConvert.DeserializeObject<UberduckFailedResponse>(resContent)?.Detail;
            throw new UberduckUnauthorizedException(detail);
        }

        if (!res.IsSuccessStatusCode)
        {
            var detail = JsonConvert.DeserializeObject<UberduckFailedResponse>(resContent)?.Detail;
            throw new UberduckFailedException(res.StatusCode, detail);
        }

        return JsonConvert.DeserializeObject<TResponse>(resContent);
    }

    private void AddAuthorization(HttpRequestMessage req)
    {
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/WWW-Authenticate#basic_authentication
        var raw = Encoding.UTF8.GetBytes($"{ApiKey}:{ApiSecret}");
        var encodedAuth = Convert.ToBase64String(raw);
        req.Headers.Add("Authorization", $"Basic {encodedAuth}");
    }

    private bool IsAuthorizationSet()
    {
        return ApiKey.Length > 0 && ApiSecret.Length > 0;
    }

    private class UberduckSpeechRequest
    {
        [JsonProperty("speech")] public string Speech { get; init; }

        [JsonProperty("voice")] public string Voice { get; init; }
    }

    private class UberduckSpeechResponse
    {
        [JsonProperty("uuid")] public string Uuid { get; init; }
    }

    private class UberduckFailedResponse
    {
        [JsonProperty("detail")] public string Detail { get; set; }
    }

    private class UberduckSpeechStatusResponse
    {
        [JsonProperty("started_at")] public DateTime StartedAt { get; init; }

        [JsonProperty("failed_at")] public DateTime? FailedAt { get; init; }

        [JsonProperty("finished_at")] public DateTime? FinishedAt { get; init; }

        [JsonProperty("path")] public string Path { get; init; }
    }
}