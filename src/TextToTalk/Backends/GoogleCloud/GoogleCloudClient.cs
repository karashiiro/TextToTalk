using Google.Cloud.TextToSpeech.V1;
using System;
using System.Collections.Generic;
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

        // Fetch all available voices
        var response = client.ListVoices("");

        var fetchedVoices = new Dictionary<string, dynamic>();

        foreach (var voice in response.Voices)
        {
            // Filter: Only include voices with "Chirp3" or "Chirp-HD" in their name
            // Rebranded "Journey" voices also now fall under "Chirp-HD"
            if (voice.Name.Contains("Chirp3") || voice.Name.Contains("Chirp-HD"))
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
            // 1. Open the stream with the cancellation token
            using var streamingCall = client.StreamingSynthesize();

            // 2. FIRST request: Configuration ONLY
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
                        // Linear16 is the 2026 standard for Chirp 3 HD PCM streaming
                        AudioEncoding = AudioEncoding.Pcm,
                        SampleRateHertz = 24000,
                        SpeakingRate = speed ?? 1.0f,
                    }
                }
            };

            // Pass token to WriteAsync to stop sending if cancelled
            await streamingCall.WriteAsync(configRequest);

            // 3. SECOND request: Input Text ONLY
            await streamingCall.WriteAsync(new StreamingSynthesizeRequest
            {
                Input = new StreamingSynthesisInput { Text = text }
            });

            await streamingCall.WriteCompleteAsync();

            // 4. Process the response stream with the cancellation token
            // Use WithCancellation to properly dispose of the enumerator on cancel
            await foreach (var response in streamingCall.GetResponseStream().WithCancellation(ct))
            {
                if (response.AudioContent.Length > 0)
                {
                    var chunkStream = new MemoryStream(response.AudioContent.ToByteArray());

                    // Note: Linear16 audio is typically handled as StreamFormat.Pcm 
                    // but matches Wave if your queue expects raw headerless bytes.
                    soundQueue.EnqueueSound(chunkStream, source, volume, StreamFormat.Wave, null);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Handle normal cancellation (e.g., stopping the voice)
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
        {
            // Handle gRPC specific cancellation
        }
    }
}