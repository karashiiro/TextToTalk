using System;
using Microsoft.Win32;

namespace VoiceUnlocker
{
    public static class Program
    {
        private const string SpeechOneCoreTokensPath = @"SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens";
        private const string SpeechTokensPath = @"SOFTWARE\Microsoft\Speech\Voices\Tokens";
        private const string SpeechSysWOW64TokensPath = @"SOFTWARE\WOW6432Node\Microsoft\SPEECH\Voices\Tokens";

        public static void Main(string[] args)
        {
            // Open mobile voices registry
            using var speechOneCoreTokens = Registry.LocalMachine.OpenSubKey(SpeechOneCoreTokensPath);
            if (speechOneCoreTokens == null)
            {
                Console.WriteLine("No mobile voices found!");
                return;
            };

            // Create/open x64 voices registry
            using var speechTokens = Registry.LocalMachine.CreateSubKey(SpeechTokensPath);

            // Create/open x86_64 voices registry
            using var sysWOW64SpeechTokens = Registry.LocalMachine.CreateSubKey(SpeechSysWOW64TokensPath);

            // Copy mobile voice info into x86_64 and x64 registry keys
            foreach (var voice in speechOneCoreTokens.GetSubKeyNames())
            {
                Console.WriteLine($"Copying {voice} from mobile voices to desktop voices...");

                using var mobileVoiceInfo = speechOneCoreTokens.OpenSubKey(voice);
                using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);

                CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);
                CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
            }

            Console.WriteLine("Done!");
        }

        private static void CopyRegistryKey(RegistryKey src, RegistryKey dst)
        {
            // Copy registry key values
            foreach (var valueName in src.GetValueNames())
            {
                dst.SetValue(valueName, src.GetValue(valueName));
            }

            // Recurse down through subkeys
            foreach (var keyName in src.GetSubKeyNames())
            {
                using var nextSrcSubKey = src.OpenSubKey(keyName);
                using var nextDstSubKey = dst.CreateSubKey(keyName);

                CopyRegistryKey(nextSrcSubKey, nextDstSubKey);
            }
        }
    }
}
