using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureClient : IDisposable
{
    private readonly SpeechConfig speechConfig;
    private readonly SpeechSynthesizer synthesizer;
    private readonly StreamingSoundQueue soundQueue;
    private readonly LexiconManager lexiconManager;
    private readonly PluginConfiguration config;
    private CancellationTokenSource? _ttsCts;

    public AzureClient(string subscriptionKey, string region, LexiconManager lexiconManager, PluginConfiguration config)
    {
        var audioConfig = AudioConfig.FromWavFileOutput("NUL");
        this.speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        this.synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
        this.soundQueue = new StreamingSoundQueue(config);
        this.lexiconManager = lexiconManager;
    }

    public TextSource GetCurrentlySpokenTextSource()
    {
        return this.soundQueue.GetCurrentlySpokenTextSource();
    }
    public List<VoiceDetails> GetVoicesWithStyles()
    {
        // Fetches the voice result asynchronously and waits for completion
        var res = this.synthesizer.GetVoicesAsync().GetAwaiter().GetResult();
        HandleResult(res);

        // Maps each voice to a custom object containing Name and StyleList
        return res.Voices.Select(voice => new VoiceDetails
        {
            Name = voice.Name,
            ShortName = voice.ShortName,
            Styles = voice.StyleList.ToList() // StyleList is a string[]
        }).ToList();
    }

    public class VoiceDetails
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public List<string> Styles { get; set; }
    }

    public List<string> GetVoices()
    {
        var res = this.synthesizer.GetVoicesAsync().GetAwaiter().GetResult();
        HandleResult(res);
        return res.Voices.Select(voice => voice.Name).ToList();
    }

    public async Task Say(string? voice, int playbackRate, float volume, TextSource source, string text, string style)
    {
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        var token = _ttsCts.Token;

        var ssml = this.lexiconManager.MakeSsml(
            text,
            style,
            voice: voice,
            langCode: "en-US",
            playbackRate: playbackRate,
            includeSpeakAttributes: true);

            // LOW LATENCY PATH: Start speaking and stream chunks immediately
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);
            using var result = await this.synthesizer.StartSpeakingSsmlAsync(ssml);
            using var audioDataStream = AudioDataStream.FromResult(result);

            byte[] buffer = new byte[4096];
            uint bytesRead;
            while ((bytesRead = audioDataStream.ReadData(buffer)) > 0)
            {
                if (token.IsCancellationRequested) break;
                // Create a copy of the buffer for the specific chunk
                var chunk = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunk, 0, (int)bytesRead);

                var chunkStream = new MemoryStream(chunk);
                this.soundQueue.EnqueueSound(chunkStream, source, volume, StreamFormat.Azure, null);
            }
            
        }


    public Task CancelAllSounds()
    {
        //this.synthesizer.Dispose();
        this.soundQueue.CancelAllSounds();
        this.soundQueue.StopHardware();
        this.soundQueue.CancelAllSounds();
        return Task.CompletedTask;
    }

    public Task CancelFromSource(TextSource source)
    {
        this.synthesizer.StopSpeakingAsync();
        this.soundQueue.StopHardware();
        this.soundQueue.CancelFromSource(source);
        return Task.CompletedTask;
    }

    private static void HandleResult(SynthesisVoicesResult res)
    {
        if (!string.IsNullOrEmpty(res.ErrorDetails))
        {
            DetailedLog.Error($"Azure request error: ({res.Reason}) \"{res.ErrorDetails}\"");
        }
    }

    private static void HandleResult(SpeechSynthesisResult res)
    {
        if (res.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(res);
            if (cancellation.Reason == CancellationReason.Error)
            {
                DetailedLog.Error($"Azure request error: ({cancellation.ErrorCode}) \"{cancellation.ErrorDetails}\"");
            }
            else
            {
                DetailedLog.Warn($"Azure request failed in state \"{cancellation.Reason}\"");
            }

            return;
        }

        if (res.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            DetailedLog.Warn($"Speech synthesis request completed in incomplete state \"{res.Reason}\"");
        }
    }

    public void Dispose()
    {
        this.synthesizer?.Dispose();
        this.soundQueue?.Dispose();
        this.soundQueue?.Dispose();
    }
}