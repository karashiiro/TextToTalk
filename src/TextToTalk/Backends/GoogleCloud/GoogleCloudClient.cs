using Google.Cloud.TextToSpeech.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudClient
{
    private TextToSpeechClient? client;
    private readonly StreamingSoundQueue? soundQueue;
    public Dictionary<string, dynamic>? Voices;
    public List<string>? Locales;

    public CancellationTokenSource? _TtsCts;

    public GoogleCloudClient(StreamingSoundQueue soundQueue, string pathToCredential)
    {
        this.soundQueue = soundQueue;
        if (pathToCredential.IsNullOrEmpty()) return;
        Init(pathToCredential);
    }

    public void Init(string pathToCredential)
    {
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", pathToCredential);
        this.client = TextToSpeechClient.Create();
        this.Voices = GetGoogleTextToSpeechVoices();
        this.Locales = ExtractUniqueLocales(Voices.Keys.ToList());
    }

    public Dictionary<string, dynamic>? GetGoogleTextToSpeechVoices()
    {
        if (client == null) return new Dictionary<string, dynamic>();

        var response = client.ListVoices("");

        var fetchedVoices = new Dictionary<string, dynamic>();

        foreach (var voice in response.Voices)
        {
            if (voice.Name.Contains("Chirp3") || voice.Name.Contains("Chirp-HD")) // Focusing on Chirp 3 and Chirp HD voices as these are the only ones enabled for streaming.  From what I can tell, this actually reduces duplicates of the same voice under different formats.
            {
                fetchedVoices.Add(voice.Name, new
                {
                    Name = voice.Name,
                    Gender = voice.SsmlGender,
                });
            }
        }

        return fetchedVoices;
    }

    public List<string> ExtractUniqueLocales(List<string>? voicesList)
    {
        if (voicesList == null) return new List<string>();
        HashSet<string> uniqueLocales = new HashSet<string>();
        foreach (string language in voicesList)
        {
            var segments = language.Split('-');
            if (segments.Length >= 2)
            {
                string locale = string.Join("-", segments.Take(2));
                uniqueLocales.Add(locale);
            } 
        }
        return uniqueLocales.ToList().OrderBy(lang => lang).ToList();
    }

    public async Task Say(string? locale, string? voice, float? speed, float volume, TextSource source, string text)
    {
        long methodStart = Stopwatch.GetTimestamp();
        if (client == null || soundQueue == null || locale == null) return;

        if (_TtsCts != null)
        {
            _TtsCts?.Cancel();
            _TtsCts?.Dispose();
        }

        _TtsCts = new CancellationTokenSource();
        var ct = _TtsCts.Token;

        try
        {
            using var streamingCall = client.StreamingSynthesize(); // One request to open the stream

            var configRequest = new StreamingSynthesizeRequest
            {
                StreamingConfig = new StreamingSynthesizeConfig
                {
                    Voice = new VoiceSelectionParams
                    {
                        LanguageCode = locale,
                        Name = voice ?? "en-US-Chirp3-HD-Puff-A"
                    },
                    StreamingAudioConfig = new StreamingAudioConfig
                    {
                        AudioEncoding = AudioEncoding.Pcm,
                        SampleRateHertz = 24000,
                        SpeakingRate = speed ?? 1.0f,
                    }
                }
            };
            await streamingCall.WriteAsync(configRequest);

            await streamingCall.WriteAsync(new StreamingSynthesizeRequest  // One request to send the text and write back the chunks
            {
                Input = new StreamingSynthesisInput { Text = text }
            });

            await streamingCall.WriteCompleteAsync();

            await foreach (var response in streamingCall.GetResponseStream().WithCancellation(ct))
            {
                if (response.AudioContent.Length > 0)
                {
                    var chunkStream = new MemoryStream(response.AudioContent.ToByteArray());
                    long? timestampToPass = methodStart;
                    soundQueue.EnqueueSound(chunkStream, source, volume, StreamFormat.Wave, null, timestampToPass);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Silent Cancellation if token is set to Cancelled
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
        {
            // Handle gRPC specific cancellation
        }
    }
}