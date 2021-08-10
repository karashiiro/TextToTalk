using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Data;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TextToTalk.Talk
{
    public static class TalkUtils
    {
        private static readonly Regex Speakable = new(@"(([⺀-⺙⺛-⻳⼀-⿕々〇〡-〩〸-〺〻㐀-䶵一-鿃豈-鶴侮-頻並-龎]+|[一-龠]+|[ぁ-ゔ]+|[ァ-ヴー]+|[a-zA-Z0-9]+|[ａ-ｚＡ-Ｚ０-９]+[々〆〤])|\s)+", RegexOptions.Compiled);

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

        public static string StripSSMLTokens(string text)
        {
            return text
                // TextToTalk#17 "<sigh>"
                .Replace("<", "")
                .Replace(">", "");
        }

        public static string NormalizePunctuation(string text)
        {
            return text
                // TextToTalk#29 emdashes
                .Replace("─", " - ") // I don't think these are the same character, but they're both used
                .Replace("—", " - ")
                // TextToTalk #41 periods
                .Replace("...", " - ");
        }

        public static bool IsSpeakable(string text)
        {
            return Speakable.Match(text).Success;
        }
    }
}