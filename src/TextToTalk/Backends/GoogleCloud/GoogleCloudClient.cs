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
            fetchedVoices.Add(voice.Name, new
            {
                Name = voice.Name,
                Gender = voice.SsmlGender,
            });
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

    public async Task Say(string? locale, string? voice, int? rate, float? speed, float volume, TextSource source, string text)
    {
        long methodStart = Stopwatch.GetTimestamp();
        if (client == null || soundQueue == null || locale == null) return;

        bool isStreamingSupported = voice != null &&
    (voice.Contains("Chirp3-HD", StringComparison.OrdinalIgnoreCase) ||
     voice.Contains("Chirp-HD", StringComparison.OrdinalIgnoreCase));

        if (_TtsCts != null)
        {
            _TtsCts?.Cancel();
            _TtsCts?.Dispose();
        }

        _TtsCts = new CancellationTokenSource();
        var ct = _TtsCts.Token;
        
        var sampleRate = rate switch
        {
            24000 => StreamFormat.Wave,
            22050 => StreamFormat.Wave22K,
            16000 => StreamFormat.Wave16K,
            8000 => StreamFormat.Wave8K,
            _ => StreamFormat.Wave22K
        };

        try
        {
            if (isStreamingSupported)
            {
                using var streamingCall = client.StreamingSynthesize();

                await streamingCall.WriteAsync(new StreamingSynthesizeRequest
                {
                    StreamingConfig = new StreamingSynthesizeConfig
                    {
                        Voice = new VoiceSelectionParams { LanguageCode = locale, Name = voice },
                        StreamingAudioConfig = new StreamingAudioConfig
                        {
                            AudioEncoding = AudioEncoding.Pcm,
                            SampleRateHertz = rate ?? 22050,
                            SpeakingRate = speed ?? 1.0f,
                        }
                    }
                });

                await streamingCall.WriteAsync(new StreamingSynthesizeRequest { Input = new StreamingSynthesisInput { Text = text } });
                await streamingCall.WriteCompleteAsync();

                await foreach (var response in streamingCall.GetResponseStream().WithCancellation(ct))
                {
                    if (response.AudioContent.Length > 0)
                    {
                        var chunkStream = new MemoryStream(response.AudioContent.ToByteArray());
                        soundQueue.EnqueueSound(chunkStream, source, volume, sampleRate, null, methodStart);
                    }
                }
            }
            else
            {
                
                var response = await client.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
                {
                    Input = new SynthesisInput { Text = text },
                    Voice = new VoiceSelectionParams { LanguageCode = locale, Name = voice },
                    AudioConfig = new AudioConfig
                    {
                        AudioEncoding = AudioEncoding.Linear16,
                        SampleRateHertz = rate ?? 22050,
                        SpeakingRate = speed ?? 1.0f,
                    }
                }, ct);

                if (response.AudioContent.Length > 0)
                {
                    var audioStream = new MemoryStream(response.AudioContent.ToByteArray());
                    soundQueue.EnqueueSound(audioStream, source, volume, sampleRate, null, methodStart);
                }
            }
        }
        catch (OperationCanceledException) { /* Silent */ }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled) { /* Silent */ }
    }
}