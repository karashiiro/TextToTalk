using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Speech.Synthesis;
using R3;

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

    public IReadOnlyDictionary<string, SelectVoiceFailedException> VoiceExceptions { get; private set; } = ImmutableDictionary<string, SelectVoiceFailedException>.Empty;

    public List<InstalledVoice> GetInstalledVoices()
    {
        return DummySynthesizer.Value == null
            ? new List<InstalledVoice>()
            : DummySynthesizer.Value.GetInstalledVoices().Where(iv => iv?.Enabled ?? false).ToList();
    }

    public IDisposable SubscribeToVoiceExceptions(Observable<SelectVoiceFailedException> voiceExceptions)
    {
        return voiceExceptions
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