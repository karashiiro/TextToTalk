using System;
using System.Speech.Synthesis;

namespace TextToTalk.Backends.System
{
    public static class SpeechSynthesizerExtensions
    {
        public static void UseVoicePreset(this SpeechSynthesizer synthesizer, VoicePreset preset)
        {
            if (preset is not SystemVoicePreset systemVoicePreset)
            {
                throw new InvalidOperationException("Invalid voice preset provided.");
            }

            synthesizer.Rate = systemVoicePreset.Rate;
            synthesizer.Volume = systemVoicePreset.Volume;
            if (synthesizer.Voice.Name != systemVoicePreset.VoiceName)
            {
                try
                {
                    synthesizer.SelectVoice(systemVoicePreset.VoiceName);
                }
                catch (Exception e)
                {
                    throw new SelectVoiceFailedException(systemVoicePreset.VoiceName,
                        $"Failed to select voice \"{systemVoicePreset.VoiceName}\".", e);
                }
            }
        }
    }
}