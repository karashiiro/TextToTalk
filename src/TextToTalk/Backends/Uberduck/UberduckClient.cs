using Dalamud.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Uberduck;

public class UberduckClient
{
    private readonly HttpClient http;
    private readonly StreamSoundQueue soundQueue;

    public string ApiKey { private get; set; }
    public string ApiSecret { private get; set; }

    public UberduckClient(StreamSoundQueue soundQueue, HttpClient http)
    {
        this.http = http;
        this.soundQueue = soundQueue;
    }

    public async Task<IList<UberduckVoice>> GetVoices()
    {
        var voicesRes = await this.http.GetStringAsync(new Uri("https://api.uberduck.ai/voices?mode=tts-basic"));
        var voices = JsonConvert.DeserializeObject<IList<UberduckVoice>>(voicesRes);
        return voices;
    }

    public async Task Say(string voice, int playbackRate, float volume, TextSource source, string text)
    {
        if (!IsAuthorizationSet())
        {
            PluginLog.LogWarning("No Uberduck authorization keys have been configured.");
            return;
        }

        var args = new UberduckSpeechRequest
        {
            Speech = text,
            Voice = voice,
        };

        using var req = new StringContent(JsonConvert.SerializeObject(args));
        AddAuthorization(req);

        var res = await this.http.PostAsync(new Uri("https://api.uberduck.ai/speak"), req);
        var content = await res.Content.ReadAsStringAsync();
        var uuid = JsonConvert.DeserializeObject<UberduckSpeechResponse>(content)?.Uuid;

        string path;
        do
        {
            await Task.Delay(100);
            var status = await GetSpeechStatus(uuid);
            path = status.Path;
        } while (string.IsNullOrEmpty(path));

        var responseStream = await this.http.GetStreamAsync(new Uri(path));
        var waveStream = new MemoryStream();
        await responseStream.CopyToAsync(waveStream);
        waveStream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(waveStream, source, StreamFormat.Wave, volume);
    }

    private async Task<UberduckSpeechStatusResponse> GetSpeechStatus(string uuid)
    {
        using var req = new StringContent("");
        AddAuthorization(req);

        var res = await this.http.PostAsync(new Uri($"https://api.uberduck.ai/speak-status?uuid={uuid}"), req);
        var content = await res.Content.ReadAsStringAsync();
        var status = JsonConvert.DeserializeObject<UberduckSpeechStatusResponse>(content);
        return status;
    }

    private void AddAuthorization(HttpContent req)
    {
        req.Headers.Add("Authorization", $"{ApiKey}:{ApiSecret}");
    }

    private bool IsAuthorizationSet()
    {
        return ApiKey.Length > 0 && ApiSecret.Length > 0;
    }

    private class UberduckSpeechRequest
    {
        [JsonProperty("speech")]
        public string Speech { get; init; }

        [JsonProperty("voice")]
        public string Voice { get; init; }
    }

    private class UberduckSpeechResponse
    {
        [JsonProperty("uuid")]
        public string Uuid { get; init; }
    }

    private class UberduckSpeechStatusResponse
    {
        [JsonProperty("started_at")]
        public DateTime StartedAt { get; init; }

        [JsonProperty("failed_at")]
        public DateTime FailedAt { get; init; }

        [JsonProperty("finished_at")]
        public DateTime FinishedAt { get; init; }

        [JsonProperty("path")]
        public string Path { get; init; }
    }
}