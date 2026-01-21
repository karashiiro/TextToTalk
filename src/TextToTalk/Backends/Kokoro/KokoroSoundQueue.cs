using Dalamud.Game;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Kokoro;

public class KokoroSoundQueue : SoundQueue<KokoroSourceQueueItem>
{
    private static readonly WaveFormat WaveFormat = new(24000, 16, 1);
    private readonly object soundLock = new();
    private readonly PluginConfiguration config;
    private readonly Task<KokoroModel> modelTask;
    private readonly LatencyTracker latencyTracker;

    private WasapiOut? soundOut;
    private BufferedWaveProvider? bufferedProvider;

    public KokoroSoundQueue(PluginConfiguration config, Task<KokoroModel> modelTask, LatencyTracker latencyTracker)
    {
        this.config = config;
        this.modelTask = modelTask;
        this.latencyTracker = latencyTracker;
    }

    private bool TryGetModel([NotNullWhen(true)] out KokoroModel? model)
    {
        if (modelTask.IsCompletedSuccessfully)
        {
            model = modelTask.Result;
            return true;
        }
        model = null;
        return false;
    }

    protected override void OnSoundLoop(KokoroSourceQueueItem nextItem)
    {
        if (!TryGetModel(out var model) || nextItem.Aborted) return;

        lock (this.soundLock)
        {
            if (this.soundOut == null)
            {
                var mmDevice = GetWasapiDeviceFromGuid(config.SelectedAudioDeviceGuid);
                this.bufferedProvider = new BufferedWaveProvider(WaveFormat)
                {
                    ReadFully = false,
                    BufferDuration = TimeSpan.FromSeconds(30),
                    DiscardOnBufferOverflow = true
                };
                this.soundOut = new WasapiOut(mmDevice, AudioClientShareMode.Shared, false, 50);
                this.soundOut.Init(this.bufferedProvider);
            }
        }

        string langCode = nextItem.Language switch
        {
            ClientLanguage.Japanese => "ja",
            ClientLanguage.German => "de",
            ClientLanguage.French => "fr",
            _ => config.KokoroUseAmericanEnglish ? "en-us" : "en",
        };

        int[] tokens = Tokenizer.Tokenize(nextItem.Text, langCode, preprocess: true);
        var segments = SegmentationSystem.SplitToSegments(tokens, new() { MaxFirstSegmentLength = 200 });

        foreach (var chunk in segments)
        {
            if (nextItem.Aborted) break;

            var samples = model.Infer(chunk, nextItem.Voice.Features, nextItem.Speed);
            byte[] bytes = KokoroPlayback.GetBytes(samples);

            // POST-INFERENCE ABORT CHECK: Prevent enqueuing "zombie" audio
            if (nextItem.Aborted) break;

            lock (this.soundLock)
            {
                if (this.bufferedProvider != null && this.soundOut != null)
                {
                    this.bufferedProvider.AddSamples(bytes, 0, bytes.Length);
                    if (this.soundOut.PlaybackState != PlaybackState.Playing)
                    {
                        if (nextItem.StartTime.HasValue)
                        {
                            var elapsed = Stopwatch.GetElapsedTime(nextItem.StartTime.Value);
                            this.latencyTracker.AddLatency(elapsed.TotalMilliseconds);
                            Log.Debug("Total Latency (Say -> Play): {Ms}", elapsed.TotalMilliseconds);
                        }
                        this.soundOut.Play();
                    }
                }
            }
        }

        // 4. Wait for audio to finish playing if not aborted
        while (!nextItem.Aborted && this.bufferedProvider?.BufferedBytes > 0)
        {
            Thread.Sleep(50);
        }
    }

    protected override void OnSoundCancelled()
    {
        GetCurrentItem()?.Cancel();

        StopHardware();
    }

    private void StopHardware()
    {
        lock (this.soundLock)
        {
            if (this.soundOut != null)
            {
                this.soundOut.Stop();
                this.soundOut.Dispose();
                this.soundOut = null;
            }
            if (this.bufferedProvider != null)
            {
                this.bufferedProvider.ClearBuffer();
                this.bufferedProvider = null;
            }
        }
    }

    private MMDevice GetWasapiDeviceFromGuid(Guid targetGuid)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in devices)
        {
            if (device.Properties.Contains(PropertyKeys.PKEY_AudioEndpoint_GUID))
            {
                var guidString = device.Properties[PropertyKeys.PKEY_AudioEndpoint_GUID].Value as string;
                if (Guid.TryParse(guidString, out var deviceGuid) && deviceGuid == targetGuid)
                    return device;
            }
        }
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) StopHardware();
        base.Dispose(disposing);
    }

    public void EnqueueSound(KokoroSourceQueueItem item)
    {
        this.AddQueueItem(item);
    }
}





public class KokoroSourceQueueItem : SoundQueueItem
{
    public KokoroSourceQueueItem(string text, KokoroVoice voice, float speed, float volume, TextSource source, ClientLanguage language, long? startTime)
    {
        Source = source;
        Text = text;
        Voice = voice;
        Speed = speed;
        Volume = volume;
        Source = source;
        Language = language;
        StartTime = startTime;
    }

    public string Text { get; }
    public KokoroVoice Voice { get; }
    public float Speed { get; }
    public float Volume { get; }
    public bool Aborted { get; private set; }
    public ClientLanguage Language { get; }

    public long? StartTime { get; set; } // Use GetTimestamp() value

    internal void Cancel()
    {
        Aborted = true;
    }
}
