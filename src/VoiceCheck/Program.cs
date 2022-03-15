using System;
using System.Linq;
using System.Speech.Synthesis;

namespace VoiceCheck
{
    internal class Program
    {
        public static void Main()
        {
            using var ss = new SpeechSynthesizer();
            var voices = ss.GetInstalledVoices()
                .Where(iv => iv?.Enabled ?? false)
                .Select(v => v.VoiceInfo?.Name)
                .ToArray();
            var loadStatus = voices.Select(v =>
            {
                try
                {
                    ss.SelectVoice(v);
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ToArray();

            var header = $"{"Voice",-30} | Loaded";
            Console.WriteLine(header);
            Console.WriteLine(new string('-', 31) + '+' + new string('-', header.Length - 31));
            Console.Write(voices.Zip(loadStatus).Aggregate("", (agg, next) => agg + $"{next.First,-30} | {next.Second}\n"));
        }
    }
}
