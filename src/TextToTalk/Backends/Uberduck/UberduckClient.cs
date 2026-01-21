using Dalamud.Game.ClientState.Fates;
using Dalamud.Interface.Windowing;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WindowsSystem = System.Net;

namespace TextToTalk.Backends.Uberduck;

public partial class UberduckClient
{
    private const string UrlBase = "https://api.uberduck.ai/v1";

    private readonly HttpClient http;
    private readonly StreamingSoundQueue soundQueue;
    public CancellationTokenSource? _ttsCts;

    public string? ApiKey { private get; set; }
    public string? ApiSecret { private get; set; }

    public IDictionary<string, IList<UberduckVoice>> CachedVoices { get; private set; }
    = new Dictionary<string, IList<UberduckVoice>>();

    private IList<UberduckVoice> Voices { get; set; }


    public UberduckClient(StreamingSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;
        var credentials = UberduckCredentialManager.LoadCredentials();
        if (credentials != null)
        {
            this.ApiKey = credentials.Password; ;
        }

        Voices = new List<UberduckVoice>();
    }

    // Uberduck TTS API call.  They have moved to a versioned API so this section needed a pretty extensive re-work
    public async Task Say(string voice, int playbackRate, float volume, TextSource source, string text)
    {

        long methodStart = Stopwatch.GetTimestamp();
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;
        var url = "https://api.uberduck.ai/v1/text-to-speech";

        var payload = new
        {
            text = text,
            voice = voice,
            output_format = "wav" 
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new WindowsSystem.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

        var response = await this.http.SendAsync(request, token);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(token);
            var result = JsonConvert.DeserializeObject<UberduckTtsResponse>(json);

            if (result?.AudioUrl != null && !token.IsCancellationRequested)
            {

                var audioBytes = await this.http.GetByteArrayAsync(result.AudioUrl);

                var waveStream = new MemoryStream(audioBytes);
                long? timestampToPass = methodStart;

                this.soundQueue.EnqueueSound(waveStream, source, volume, StreamFormat.Uberduck, null, timestampToPass);
            }
        }
    }
    public class UberduckTtsResponse
    {
        [JsonProperty("audio_url")]
        public string? AudioUrl { get; set; }
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

    public async Task<IDictionary<string, IList<UberduckVoice>>> UpdateVoices()
    {
        Log.Information("Updating Voices...");
        if (IsAuthorizationSet())
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.uberduck.ai/v1/voices");
            AddAuthorization(request);

            var response = await this.http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<UberduckVoiceResponse>(json);

                this.Voices = result?.Voices ?? new List<UberduckVoice>();

                this.CachedVoices = this.Voices
                    .OrderBy(v => v.DisplayName)
                    .GroupBy(v => v.Category ?? "Uncategorized")
                    .ToDictionary(
                        g => g.Key,
                        g => (IList<UberduckVoice>)g.ToList()
                    );
                return this.CachedVoices;
            }
            else
            {
                Log.Information($"Response = {response.StatusCode}");
            }
        }

        return new Dictionary<string, IList<UberduckVoice>>();
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
        if (res.StatusCode is WindowsSystem.HttpStatusCode.Unauthorized or WindowsSystem.HttpStatusCode.Forbidden)
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
        req.Headers.Authorization = new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
    }

    private bool IsAuthorizationSet()
    {
        var resultbool = ApiKey?.Length > 0;
        Log.Information($"Is Authorization Set? {resultbool}");
        return resultbool;// && ApiSecret?.Length > 0;
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