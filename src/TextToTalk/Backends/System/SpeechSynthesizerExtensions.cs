using System;
using System.Speech.Synthesis;

namespace TextToTalk.Backends.System
{
    public static class SpeechSynthesizerExtensions
    {
        public static void UseVoicePreset(this SpeechSynthesizer synthesizer, VoicePreset preset)
        {
            synthesizer.Rate = preset.Rate;
            synthesizer.Volume = preset.Volume;
            if (synthesizer.Voice.Name != preset.VoiceName)
            {
                try
                {
                    synthesizer.SelectVoice(preset.VoiceName);
                }
                catch (Exception e)
                {
                    throw new SelectVoiceFailedException(preset.VoiceName,
                        $"Failed to select voice \"{preset.VoiceName}\".", e);
                }
            }
        }
    }
}