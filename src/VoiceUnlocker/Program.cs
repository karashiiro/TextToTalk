﻿using Microsoft.Win32;
using System;

namespace VoiceUnlocker
{
    public static class Program
    {

        public static void Main()
        {
            string[] addTokensPaths = {
                @"Speech_OneCore\Voices\Tokens",
                @"Speech_OneCore\CortanaVoices\Tokens",
                @"Speech Server\v11.0\Voices\Tokens",
            };
            bool addVoiceFound = false;
            const string speechTokensPath = @"Speech\Voices\Tokens";
            const string speechSysWOW64TokensPath = @"SPEECH\Voices\Tokens";
            const string x64Prefix = @"SOFTWARE\WOW6432Node\Microsoft\";
            const string x86Prefix = @"SOFTWARE\Microsoft\";

            // Create/open voices registry
            Microsoft.Win32.RegistryKey[] speechTokens = {
                    Registry.LocalMachine.CreateSubKey(x64Prefix + speechTokensPath),
                    Registry.LocalMachine.CreateSubKey(x86Prefix + speechSysWOW64TokensPath),
            };

            foreach (var tokensPath in addTokensPaths)
            {
                // Open additional voices registry
                Microsoft.Win32.RegistryKey[] addTokens = {
                    Registry.LocalMachine.OpenSubKey(x64Prefix + tokensPath),
                    Registry.LocalMachine.OpenSubKey(x86Prefix + tokensPath),
                };

                foreach (var i in addTokens)
                {
                    // Copy voice info into registry keys
                    if (i is not null)
                    {
                        foreach (var addVoice in i.GetSubKeyNames())
                        {
                            using var addVoiceInfo = i.OpenSubKey(addVoice);

                            addVoiceFound = true;
                            Console.WriteLine($"Copying {addVoice} to desktop voices...");

                            foreach (var j in speechTokens)
                            {
                                using var speechVoiceInfo = j?.CreateSubKey(addVoice);
                                CopyRegistryKey(addVoiceInfo, speechVoiceInfo);
                            }
                        }
                    }
                }
            }

            if (addVoiceFound is false)
            {
                Console.WriteLine("No additional voices found!");
                return;
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