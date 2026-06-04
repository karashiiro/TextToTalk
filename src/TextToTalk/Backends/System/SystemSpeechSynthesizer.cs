using System.IO;
using System.Speech.Synthesis;

namespace TextToTalk.Backends.System;

public class SystemSpeechSynthesizer : ISpeechSynthesizer
{
    private readonly SpeechSynthesizer synthesizer = new();

    public int Rate
    {
        get => this.synthesizer.Rate;
        set => this.synthesizer.Rate = value;
    }

    public int Volume
    {
        get => this.synthesizer.Volume;
        set => this.synthesizer.Volume = value;
    }

    public string? VoiceName => this.synthesizer.Voice?.Name;

    public string? VoiceCultureIetfLanguageTag => this.synthesizer.Voice?.Culture?.IetfLanguageTag;

    public void SelectVoice(string name) => this.synthesizer.SelectVoice(name);

    public void SetOutputToWaveStream(Stream stream) => this.synthesizer.SetOutputToWaveStream(stream);

    public void SetOutputToNull() => this.synthesizer.SetOutputToNull();

    public void SpeakSsml(string ssml) => this.synthesizer.SpeakSsml(ssml);

    public void Dispose() => this.synthesizer.Dispose();
}
