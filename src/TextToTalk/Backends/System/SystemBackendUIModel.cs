using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Speech.Synthesis;

namespace TextToTalk.Backends.System;

public class SystemBackendUIModel
{
    private static readonly Lazy<SpeechSynthesizer?> DummySynthesizer = new(() =>
    {
        try
        {
            return new SpeechSynthesizer();
        }
        catch (Exception e)
        {
            DetailedLog.Error(e, "Failed to create speech synthesizer.");
            return null;
        }
    });

    public IReadOnlyDictionary<string, SelectVoiceFailedException> VoiceExceptions { get; private set; }

    public SystemBackendUIModel()
    {
        VoiceExceptions = ImmutableDictionary<string, SelectVoiceFailedException>.Empty;
    }

    public List<InstalledVoice> GetInstalledVoices()
    {
        return DummySynthesizer.Value == null
            ? new List<InstalledVoice>()
            : DummySynthesizer.Value.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
    }

    public IDisposable SubscribeToVoiceExceptions(IObservable<SelectVoiceFailedException> voiceExceptions)
    {
        return voiceExceptions
            .SubscribeOn(TaskPoolScheduler.Default)
            .Subscribe(exc =>
            {
                var imm = ImmutableDictionary.CreateRange(VoiceExceptions.Append(
                    new KeyValuePair<string, SelectVoiceFailedException>(exc.VoiceId ?? "", exc)));
                VoiceExceptions = imm;
            });
    }

    public void DismissVoiceException(string voiceId)
    {
        var imm = ImmutableDictionary.CreateRange(VoiceExceptions.Where(kvp => kvp.Key != voiceId));
        VoiceExceptions = imm;
    }
}