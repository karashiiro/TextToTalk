using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Game;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;

namespace TextToTalk.Backends.Kokoro;

public class KokoroSoundQueue : SoundQueue<KokoroSourceQueueItem>
{
    private readonly KokoroPlayback playback = new();
    private readonly StreamSoundQueue streamSoundQueue;
    private readonly PluginConfiguration config;
    private readonly Task<KokoroModel> modelTask;

    public KokoroSoundQueue(PluginConfiguration config, Task<KokoroModel> modelTask)
    {
        this.config = config;
        this.modelTask = modelTask;
        this.streamSoundQueue = new StreamSoundQueue(config);
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

    public void EnqueueSound(KokoroSourceQueueItem item)
    {
        this.AddQueueItem(item);
    }

    protected override void OnSoundCancelled()
    {
        GetCurrentItem()?.Cancel();
    }

    public override void CancelAllSounds()
    {
        base.CancelAllSounds();
        streamSoundQueue.CancelAllSounds();
    }

    public override void CancelFromSource(TextSource source)
    {
        base.CancelFromSource(source);
        streamSoundQueue.CancelFromSource(source);
    }

    protected override void OnSoundLoop(KokoroSourceQueueItem nextItem)
    {
        if (!TryGetModel(out var model) || nextItem.Aborted)
        {
            return;
        }

        var lang = nextItem.Language;

        // https://github.com/espeak-ng/espeak-ng/blob/master/docs/languages.md
        string langCode = lang switch
        {
            ClientLanguage.Japanese => "ja",
            ClientLanguage.English => "en",
            ClientLanguage.German => "de",
            ClientLanguage.French => "fr",
            _ => "en",
        };

        if (langCode == "en" && config.KokoroUseAmericanEnglish)
        {
            langCode = "en-us"; // Use American English for English language
        }

        // this is a blocking call!
        int[] tokens = Tokenizer.Tokenize(nextItem.Text, langCode, preprocess: true);
        if (nextItem.Aborted)
        {
            return;
        }

        var tokensList = SegmentationSystem.SplitToSegments(tokens, new()
        {
            MinFirstSegmentLength = 20,
            MaxFirstSegmentLength = 200,
            MaxSecondSegmentLength = 200
        }); // Split tokens into chunks Kokoro can handle

        foreach (var tokenChunk in tokensList)
        {
            // this is a blocking call!
            var samples = model.Infer(tokenChunk, nextItem.Voice.Features, nextItem.Speed);
            if (nextItem.Aborted)
            {
                return;
            }

            var bytes = KokoroPlayback.GetBytes(samples);
            var ms = new MemoryStream(bytes);
            streamSoundQueue.EnqueueSound(ms, nextItem.Source, StreamFormat.Raw, nextItem.Volume);
        }
    }
}

public class KokoroSourceQueueItem : SoundQueueItem
{
    public KokoroSourceQueueItem(string text, KokoroVoice voice, float speed, float volume, TextSource source, ClientLanguage language)
    {
        Source = source;
        Text = text;
        Voice = voice;
        Speed = speed;
        Volume = volume;
        Source = source;
        Language = language;
    }

    public string Text { get; }
    public KokoroVoice Voice { get; }
    public float Speed { get; }
    public float Volume { get; }
    public bool Aborted { get; private set; }
    public ClientLanguage Language { get; }

    internal void Cancel()
    {
        Aborted = true;
    }
}
