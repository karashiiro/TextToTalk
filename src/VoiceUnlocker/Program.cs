using Microsoft.Win32;
using System;

namespace VoiceUnlocker
{
    public static class Program
    {
        private const string SpeechOneCoreTokensPath = @"SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens";
        private const string SpeechOneCoreSysWOW64TokensPath = @"SOFTWARE\WOW6432Node\Microsoft\Speech_OneCore\Voices\Tokens";
        private const string SpeechOneCoreCortanaTokensPath = @"SOFTWARE\Microsoft\Speech_OneCore\CortanaVoices\Tokens";
        private const string SpeechOneCoreCortanaSysWOW64TokensPath = @"SOFTWARE\WOW6432Node\Microsoft\Speech_OneCore\CortanaVoices\Tokens";
        private const string SpeechServerTokensPath = @"SOFTWARE\Microsoft\Speech Server\v11.0\Voices\Tokens";
        private const string SpeechServerSysWOW64TokensPath = @"SOFTWARE\WOW6432Node\Microsoft\Speech Server\v11.0\Voices\Tokens";
        private const string SpeechTokensPath = @"SOFTWARE\Microsoft\Speech\Voices\Tokens";
        private const string SpeechSysWOW64TokensPath = @"SOFTWARE\WOW6432Node\Microsoft\SPEECH\Voices\Tokens";

        public static void Main()
        {
            // Open mobile voices registry
            using var speechOneCoreTokens = Registry.LocalMachine.OpenSubKey(SpeechOneCoreTokensPath);
            using var sysWOW64SpeechOneCoreTokens = Registry.LocalMachine.OpenSubKey(SpeechOneCoreSysWOW64TokensPath);
            using var speechOneCoreCortanaTokens = Registry.LocalMachine.OpenSubKey(SpeechOneCoreCortanaTokensPath);
            using var sysWOW64SpeechOneCoreCortanaTokens = Registry.LocalMachine.OpenSubKey(SpeechOneCoreCortanaSysWOW64TokensPath);
            using var speechServerTokens = Registry.LocalMachine.OpenSubKey(SpeechServerTokensPath);
            using var sysWOW64SpeechServerTokens = Registry.LocalMachine.OpenSubKey(SpeechServerSysWOW64TokensPath);

            if (speechOneCoreTokens == null && speechOneCoreCortanaTokens == null)
            {
                Console.WriteLine("No additional voices found!");
                return;
            }

            // Create/open x64 voices registry
            using var speechTokens = Registry.LocalMachine.CreateSubKey(SpeechTokensPath);

            // Create/open x86_64 voices registry
            using var sysWOW64SpeechTokens = Registry.LocalMachine.CreateSubKey(SpeechSysWOW64TokensPath);

            // Copy x64 mobile voice info into x86_64 and x64 registry keys
            if (speechOneCoreTokens is not null)
            {
                foreach (var voice in speechOneCoreTokens.GetSubKeyNames())
                {
                    Console.WriteLine($"Copying {voice} from mobile voices to desktop voices...");

                    using var mobileVoiceInfo = speechOneCoreTokens.OpenSubKey(voice);

                    using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);

                    using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
                }
            }

            // Copy x86_64 mobile voice info into x86_64 and x64 registry keys
            if (sysWOW64SpeechOneCoreTokens is not null)
            {
                foreach (var voice in speechOneCoreCortanaTokens.GetSubKeyNames())
                {
                    Console.WriteLine($"Copying {voice} from mobile Cortana voices to desktop voices...");

                    using var mobileVoiceInfo = speechOneCoreCortanaTokens.OpenSubKey(voice);

                    using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);

                    using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
                }
            }

            // Copy x64 mobile Cortana voice info into x86_64 and x64 registry keys
            if (speechOneCoreCortanaTokens is not null)
            {
                foreach (var voice in speechOneCoreTokens.GetSubKeyNames())
                {
                    Console.WriteLine($"Copying {voice} from mobile voices to desktop voices...");

                    using var mobileVoiceInfo = speechOneCoreTokens.OpenSubKey(voice);

                    using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);

                    using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
                }
            }

            // Copy x86_64 mobile Cortana voice info into x86_64 and x64 registry keys
            if (sysWOW64SpeechOneCoreCortanaTokens is not null)
            {
                foreach (var voice in speechOneCoreTokens.GetSubKeyNames())
                {
                    Console.WriteLine($"Copying {voice} from mobile voices to desktop voices...");

                    using var mobileVoiceInfo = speechOneCoreTokens.OpenSubKey(voice);

                    using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);

                    using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
                }
            }

            // Copy x64 speech platform voice info into x86_64 and x64 registry keys
            if (speechServerTokens is not null)
            {
                foreach (var voice in speechOneCoreTokens.GetSubKeyNames())
                {
                    Console.WriteLine($"Copying {voice} from mobile voices to desktop voices...");

                    using var mobileVoiceInfo = speechOneCoreTokens.OpenSubKey(voice);

                    using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);

                    using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
                }
            }

            // Copy x86_64 speech platform info into x86_64 and x64 registry keys
            if (sysWOW64SpeechServerTokens is not null)
            {
                foreach (var voice in speechOneCoreCortanaTokens.GetSubKeyNames())
                {
                    Console.WriteLine($"Copying {voice} from mobile Cortana voices to desktop voices...");

                    using var mobileVoiceInfo = speechOneCoreCortanaTokens.OpenSubKey(voice);

                    using var x64VoiceInfo = speechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x64VoiceInfo);

                    using var x86VoiceInfo = sysWOW64SpeechTokens?.CreateSubKey(voice);
                    CopyRegistryKey(mobileVoiceInfo, x86VoiceInfo);
                }
            }

            Console.WriteLine("Done!");
        }

        private static void CopyRegistryKey(RegistryKey src, RegistryKey dst)
        {
            // Copy registry key values
            foreach (var valueName in src.GetValueNames())
            {
                // All of the voice keys have environment variables embedded in
                // their paths - it's probably fine if those get expanded, but
                // better to be safe than sorry
                var value = src.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var valueKind = src.GetValueKind(valueName);
                if (value != null)
                {
                    dst.SetValue(valueName, value, valueKind);
                }
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
