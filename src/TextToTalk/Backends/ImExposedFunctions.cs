using System;

namespace TextToTalk.Backends
{
    /// <summary>
    /// This class is a violation of encapsulation, but it lets me clean things up
    /// more effectively so I won't lose sleep over it.
    /// </summary>
    public class ImExposedFunctions
    {
        public Action OpenVoiceUnlockerWindow { get; set; }
    }
}