using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Logging;
using Microsoft.CognitiveServices.Speech;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Azure;

public class AzureClient : IDisposable
{
    private readonly SpeechConfig config;
    private readonly SpeechSynthesizer synthesizer;
    private readonly StreamSoundQueue soundQueue;
    private readonly LexiconManager lexiconManager;

    public AzureClient(string subscriptionKey, string region, LexiconManager lexiconManager)
    {
        this.config = SpeechConfig.FromSubscription(subscriptionKey, region);
        this.synthesizer = new SpeechSynthesizer(config);
        this.soundQueue = new StreamSoundQueue();
        this.lexiconManager = lexiconManager;
    }

    public TextSource GetCurrentlySpokenTextSource()
    {
        return this.soundQueue.GetCurrentlySpokenTextSource();
    }

    public IList<string> GetVoices()
    {
        var res = this.synthesizer.GetVoicesAsync().GetAwaiter().GetResult();
        return res.Voices.Select(voice => voice.Name).ToList();
    }

    public async Task Say(string voice, int playbackRate, float volume, TextSource source, string text)
    {
        var ssml = this.lexiconManager.MakeSsml(text, playbackRate: playbackRate, includeSpeakAttributes: false);
        PluginLog.Log(ssml);

        this.config.SpeechSynthesisVoiceName = voice;
        var res = await this.synthesizer.SpeakSsmlAsync(ssml);

        HandleResult(res, volume, source);
    }

    private void HandleResult(SpeechSynthesisResult res, float volume, TextSource source)
    {
        if (res.Reason != ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(res);
            if (cancellation.Reason == CancellationReason.Error)
            {
                PluginLog.LogError($"Speech request error: ({cancellation.ErrorCode}) \"{cancellation.ErrorDetails}\"");
            }
            else
            {
                PluginLog.LogWarning($"Speech request failed in state \"{cancellation.Reason}\"");
            }

            return;
        }

        if (res.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            PluginLog.LogWarning($"Speech request completed in incomplete state \"{res.Reason}\"");
            return;
        }

        var soundStream = new MemoryStream(res.AudioData);
        soundStream.Seek(0, SeekOrigin.Begin);

        this.soundQueue.EnqueueSound(soundStream, source, StreamFormat.Mp3, volume);
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

    public void Dispose()
    {
        this.synthesizer.Dispose();
        this.soundQueue.Dispose();
    }
}