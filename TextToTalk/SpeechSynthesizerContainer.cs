using System;
using System.Speech.Synthesis;

namespace TextToTalk
{
    public class SpeechSynthesizerContainer : IDisposable
    {
        public SpeechSynthesizer Synthesizer { get; private set; }

        public SpeechSynthesizerContainer()
        {
            Synthesizer = new SpeechSynthesizer();
        }

        public void RegenerateSynthesizer()
        {
            Synthesizer?.Dispose();
            Synthesizer = new SpeechSynthesizer();
        }

        public void Dispose()
        {
            Synthesizer?.Dispose();
        }
    }
}