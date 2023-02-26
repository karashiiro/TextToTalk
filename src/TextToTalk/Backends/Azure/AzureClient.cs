using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureClient : IDisposable
{
    private readonly SpeechConfig speechConfig;
    private readonly SpeechSynthesizer synthesizer;
    private readonly StreamSoundQueue soundQueue;
    private readonly LexiconManager lexiconManager;

    public AzureClient(string subscriptionKey, string region, LexiconManager lexiconManager)
    {
        var audioConfig = AudioConfig.FromWavFileOutput("NUL");
        this.speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        this.synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
        this.soundQueue = new StreamSoundQueue();
        this.lexiconManager = lexiconManager;
    }

    public TextSource GetCurrentlySpokenTextSource()
    {
        return this.soundQueue.GetCurrentlySpokenTextSource();
    }

    public List<string> GetVoices()
    {
        var res = this.synthesizer.GetVoicesAsync().GetAwaiter().GetResult();
        HandleResult(res);
        return res.Voices.Select(voice => voice.Name).ToList();
    }

    public async Task Say(string? voice, int playbackRate, float volume, TextSource source, string text)
    {
        var ssml = this.lexiconManager.MakeSsml(
            text,
            voice: voice,
            langCode: "en-US",
            playbackRate: playbackRate,
            includeSpeakAttributes: true);
        DetailedLog.Info(ssml);

        var res = await this.synthesizer.SpeakSsmlAsync(ssml);

        HandleResult(res);

        var soundStream = new MemoryStream(res.AudioData);
        soundStream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(soundStream, source, StreamFormat.Wave, volume);
    }

    public Task CancelAllSounds()
    {
        this.soundQueue.CancelAllSounds();
        return Task.CompletedTask;
    }

    public Task CancelFromSource(TextSource source)
    {
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
    }
}