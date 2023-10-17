using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Uberduck;

public partial class UberduckClient
{
    private const string UrlBase = "https://api.uberduck.ai";

    private readonly HttpClient http;
    private readonly StreamSoundQueue soundQueue;

    public string? ApiKey { private get; set; }
    public string? ApiSecret { private get; set; }

    private IList<UberduckVoice> Voices { get; set; }

    public UberduckClient(StreamSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;

        Voices = new List<UberduckVoice>();
    }

    public async Task<IDictionary<string, IList<UberduckVoice>>> GetVoices()
    {
        if (Voices.Count == 0) await UpdateVoices();
        return Voices
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

        ArgumentException.ThrowIfNullOrEmpty(voice);

        var voiceModelUuid = IsUuid(voice) ? voice : await GetUuidForVoice(voice);
        var args = new UberduckSpeechRequest
        {
            Speech = text,
            VoiceModelUuid = voiceModelUuid,
        };

        // Make the request
        using var content = new StringContent(JsonConvert.SerializeObject(args), Encoding.UTF8, "application/json");
        var res = await SendRequest<UberduckSpeechResponse>("/speak", reqContent: content);
        var uuid = res?.Uuid;
        if (uuid is null)
        {
            DetailedLog.Warn("Got null UUID from Uberduck");
            return;
        }

        DetailedLog.Debug($"Got request UUID {uuid} from Uberduck");

        // Poll for the TTS result
        await Task.Delay(20);

        var path = "";
        do
        {
            try
            {
                var status = await GetSpeechStatus(uuid);
                if (status is not { FailedAt: null })
                {
                    DetailedLog.Warn($"TTS request {uuid} failed for an unknown reason");
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

        DetailedLog.Debug($"Got response for TTS request {uuid}");

        // Copy the sound to a new buffer and enqueue it
        var responseStream = await this.http.GetStreamAsync(new Uri(path));
        var waveStream = new MemoryStream();
        await responseStream.CopyToAsync(waveStream);
        waveStream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(waveStream, source, StreamFormat.Wave, volume);
    }

    private Task<UberduckSpeechStatusResponse?> GetSpeechStatus(string uuid)
    {
        return SendRequest<UberduckSpeechStatusResponse>("/speak-status", $"uuid={uuid}");
    }

    [GeneratedRegex("[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex UuidRegex();

    private static bool IsUuid(string voice)
    {
        return UuidRegex().IsMatch(voice);
    }

    private async Task<string> GetUuidForVoice(string voice)
    {
        if (Voices.Count == 0) await UpdateVoices();
        var voiceInfo = Voices.Single(v => v.Name == voice);
        return voiceInfo.VoiceModelUuid;
    }

    private async Task UpdateVoices()
    {
        var voicesRes = await this.http.GetStringAsync(new Uri("https://api.uberduck.ai/voices?mode=tts-basic"));
        Voices = JsonConvert.DeserializeObject<IList<UberduckVoice>>(voicesRes) ?? new List<UberduckVoice>();
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

        if (reqContent != null)
        {
            req.Content = reqContent;
        }

        var res = await this.http.SendAsync(req);
        var resContent = await res.Content.ReadAsStringAsync();
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var detail = GetRequestFailureDetail(resContent);
            throw new UberduckUnauthorizedException(detail);
        }

        if (!res.IsSuccessStatusCode)
        {
            var detail = GetRequestFailureDetail(resContent);
            throw new UberduckFailedException(res.StatusCode, detail);
        }

        return JsonConvert.DeserializeObject<TResponse>(resContent);
    }

    private static string GetRequestFailureDetail(string resContent)
    {
        try
        {
            var detail = JsonConvert.DeserializeObject<UberduckFailedResponse>(resContent)?.Detail ??
                         new List<UberduckFailedResponseEntry>();
            return string.Join("\n", detail.Select(e => $"({e.Type}:{string.Join(",", e.Location)}) {e.Message}"));
        }
        catch (JsonReaderException ex)
        {
            throw new AggregateException(
                $"Could not parse {nameof(UberduckFailedResponse)} from string \"{resContent}\"", ex);
        }
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
        return ApiKey?.Length > 0 && ApiSecret?.Length > 0;
    }

    private class UberduckSpeechRequest
    {
        [JsonProperty("speech")] public required string Speech { get; init; }

        [JsonProperty("voicemodel_uuid")] public string? VoiceModelUuid { get; init; }
    }

    private class UberduckSpeechResponse
    {
        [JsonProperty("uuid")] public required string Uuid { get; init; }
    }

    private class UberduckFailedResponse
    {
        [JsonProperty("detail")] public required IList<UberduckFailedResponseEntry> Detail { get; set; }
    }

    private class UberduckFailedResponseEntry
    {
        [JsonProperty("loc")] public required IList<string> Location { get; set; }

        [JsonProperty("msg")] public required string Message { get; set; }

        [JsonProperty("type")] public required string Type { get; set; }
    }

    private class UberduckSpeechStatusResponse
    {
        [JsonProperty("started_at")] public DateTime StartedAt { get; init; }

        [JsonProperty("failed_at")] public DateTime? FailedAt { get; init; }

        [JsonProperty("finished_at")] public DateTime? FinishedAt { get; init; }

        [JsonProperty("path")] public required string Path { get; init; }
    }
}