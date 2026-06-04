using System;
using System.IO;

namespace TextToTalk.Backends.System;

/// <summary>
/// Abstraction over <see cref="System.Speech.Synthesis.SpeechSynthesizer"/> to enable
/// unit testing of the System backend's sound queue without requiring a real SAPI voice.
/// </summary>
public interface ISpeechSynthesizer : IDisposable
{
    int Rate { get; set; }
    int Volume { get; set; }
    string? VoiceName { get; }
    string? VoiceCultureIetfLanguageTag { get; }
    void SelectVoice(string name);
    void SetOutputToWaveStream(Stream stream);
    void SetOutputToNull();
    void SpeakSsml(string ssml);
}
