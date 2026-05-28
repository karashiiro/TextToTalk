using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

    private readonly Lock synthesisLock = new();

    // Dispose signals this counter so the countdown can complete once the last in-flight synthesis request finishes.
    private readonly CountdownEvent synthesisCountdown = new(initialCount: 1);
    private bool disposed;

    public AzureClient(string subscriptionKey, string region, LexiconManager lexiconManager, PluginConfiguration config)
    {
        var audioConfig = AudioConfig.FromWavFileOutput("NUL");
        this.speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
        this.synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
        this.soundQueue = new StreamSoundQueue(config);
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
            Styles = voice.StyleList.ToList(), // StyleList is a string[]
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
        if (!TryBeginSynthesis())
        {
            DetailedLog.Verbose("Azure client is disposed; dropping synthesis request.");
            return;
        }

        try
        {
            var ssml = this.lexiconManager.MakeSsml(
                text,
                style,
                voice: voice,
                langCode: "en-US",
                playbackRate: playbackRate,
                includeSpeakAttributes: true);
            DetailedLog.Verbose(ssml);

            // ConfigureAwait(false) keeps the continuation off any captured context so a
            // blocking Dispose cannot deadlock against this operation's completion.
            var res = await this.synthesizer.SpeakSsmlAsync(ssml).ConfigureAwait(false);

            HandleResult(res);

            var soundStream = new MemoryStream(res.AudioData);
            soundStream.Seek(0, SeekOrigin.Begin);

            this.soundQueue.EnqueueSound(soundStream, source, StreamFormat.Wave, volume);
        }
        finally
        {
            EndSynthesis();
        }
    }

    private bool TryBeginSynthesis()
    {
        lock (this.synthesisLock)
        {
            if (this.disposed)
            {
                return false;
            }

            // While not disposed, the count never drops below the initial token,
            // so AddCount cannot race the countdown to zero (which throws).
            this.synthesisCountdown.AddCount();
            return true;
        }
    }

    private void EndSynthesis()
    {
        this.synthesisCountdown.Signal();
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
        lock (this.synthesisLock)
        {
            if (this.disposed)
            {
                return;
            }

            // Blocks new synthesis requests so the in-flight ones can drain
            this.disposed = true;
        }

        // Release the initial token, then wait for in-flight SpeakSsmlAsync calls to finish
        // before freeing the synthesizer handle. Disposing mid-synthesis frees a
        // handle the SDK is still using on a background thread, causing an access violation.
        this.synthesisCountdown.Signal();
        this.synthesisCountdown.Wait();

        this.synthesizer.Dispose();
        this.soundQueue.Dispose();
        this.synthesisCountdown.Dispose();
    }
}