using Dalamud.Data;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TextToTalk.Talk
{
    public static class TalkUtils
    {
        private static readonly Regex Speakable = new(@"\p{L}+|\p{M}+|\p{N}+", RegexOptions.Compiled);
        private static readonly Regex Stutter = new(@"(?<=\s|^)\p{L}{1,2}-", RegexOptions.Compiled);
        private static readonly Regex Bracketed = new(@"<[^<]*>", RegexOptions.Compiled);

        public static unsafe TalkAddonText ReadTalkAddon(DataManager data, AddonTalk* talkAddon)
        {
            return new()
            {
                Speaker = ReadTextNode(data, talkAddon->AtkTextNode220),
                Text = ReadTextNode(data, talkAddon->AtkTextNode228),
            };
        }

        private static SeStringManager StringManager { get; set; }

        private static unsafe string ReadTextNode(DataManager data, AtkTextNode* textNode)
        {
            if (textNode == null) return "";

            StringManager ??= new SeStringManager(data);

            var textPtr = textNode->NodeText.StringPtr;
            var textLength = textNode->NodeText.BufUsed - 1; // Null-terminated; chop off the null byte
            if (textLength <= 0) return "";

            var textBytes = new byte[textLength];
            Marshal.Copy((IntPtr)textPtr, textBytes, 0, (int)textLength);
            var seString = StringManager.Parse(textBytes);
            return seString.TextValue;
        }

        public static unsafe bool IsVisible(AddonTalk* talkAddon)
        {
            return talkAddon->AtkUnitBase.IsVisible;
        }

        public static string StripAngleBracketedText(string text)
        {
            // TextToTalk#17 "<sigh>"
            return Bracketed.Replace(text, "");
        }

        public static string ReplaceSsmlTokens(string text)
        {
            return text.Replace("&", "and");
        }

        public static string NormalizePunctuation(string text)
        {
            return text
                // TextToTalk#29 emdashes
                .Replace("─", " - ") // I don't think these are the same character, but they're both used
                .Replace("—", " - ");
        }

        /// <summary>
        /// Removes single letters with a hyphen following them, since they aren't read as expected.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <returns>The cleaned text.</returns>
        public static string RemoveStutters(string text)
        {
            while (true)
            {
                if (!Stutter.IsMatch(text)) return text;
                text = Stutter.Replace(text, "");
            }
        }

        public static bool IsSpeakable(string text)
        {
            // TextToTalk #41 Unspeakable text
            return Speakable.Match(text).Success;
        }
    }
}