﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TextToTalk.Talk;

namespace TextToTalk.Utils
{
    public static partial class TalkUtils
    {
        [GeneratedRegex(@"\p{L}+|\p{M}+|\p{N}+|\s+", RegexOptions.Compiled)]
        private static partial Regex SpeakableRegex();

        [GeneratedRegex(@"(?<=\s|^)(\p{L}{1,2})-(?=\1)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex StutterRegex();

        [GeneratedRegex("<[^<]*>", RegexOptions.Compiled)]
        private static partial Regex BracketedRegex();

        private static readonly Regex Speakable = SpeakableRegex();
        private static readonly Regex Stutter = StutterRegex();
        private static readonly Regex Bracketed = BracketedRegex();

        public static unsafe AddonTalkText ReadTalkAddon(AddonTalk* talkAddon)
        {
            return new AddonTalkText
            {
                Speaker = ReadTextNode(talkAddon->AtkTextNode220),
                Text = ReadTextNode(talkAddon->AtkTextNode228),
            };
        }

        public static unsafe AddonTalkText ReadTalkAddon(AddonBattleTalk* talkAddon)
        {
            return new AddonTalkText
            {
                Speaker = ReadTextNode(talkAddon->Speaker),
                Text = ReadTextNode(talkAddon->Text),
            };
        }

        private static unsafe string ReadTextNode(AtkTextNode* textNode)
        {
            if (textNode == null) return "";
            var seString = textNode->NodeText.StringPtr.AsDalamudSeString();
            return seString.TextValue
                .Trim()
                .Replace("\n", "")
                .Replace("\r", "");
        }

        public static string StripAngleBracketedText(string text)
        {
            // TextToTalk#17 "<sigh>"
            return Bracketed.Replace(text, "").Trim();
        }

        public static string ReplaceSsmlTokens(string text)
        {
            return text.Replace("&", "and");
        }

        public static string NormalizePunctuation(string? text)
        {
            return text?
                       // TextToTalk#29 emdashes and dashes and whatever else
                       .Replace("─", " - ") // These are not the same character
                       .Replace("—", " - ")
                       .Replace("–", "-") ??
                   ""; // Hopefully, this one is only in Kan-E-Senna's name? Otherwise, I'm not sure how to parse this correctly.
        }

        public static string StripWorldFromNames(SeString message)
        {
            // Remove world from all names in message body
            var world = "";
            var cleanString = new SeStringBuilder();
            foreach (var p in message.Payloads)
            {
                switch (p)
                {
                    case PlayerPayload pp:
                        world = pp.World.Value.Name.ToString();
                        break;
                    case TextPayload tp when world != "" && tp.Text != null && tp.Text.Contains(world):
                        cleanString.AddText(tp.Text.Replace(world, ""));
                        break;
                    default:
                        cleanString.Add(p);
                        break;
                }
            }

            return cleanString.Build().TextValue;
        }

        public static bool TryGetEntityName(SeString input, out string name)
        {
            name = string.Join("", Speakable.Matches(input.TextValue));
            foreach (var p in input.Payloads)
            {
                if (p is PlayerPayload pp)
                {
                    // Simplest case; the payload has the raw name
                    name = pp.PlayerName;
                    return true;
                }
            }

            return name != string.Empty;
        }

        /// <summary>
        /// Removes single letters with a hyphen following them, since they aren't read as expected.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <returns>The cleaned text.</returns>
        public static string RemoveStutters(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var startsCapitalized = char.IsUpper(text, 0);
            while (true)
            {
                if (!Stutter.IsMatch(text)) break;
                text = Stutter.Replace(text, "");
            }

            var isCapitalized = char.IsUpper(text, 0);
            if (startsCapitalized && !isCapitalized)
            {
                text = char.ToUpper(text[0]) + text[1..];
            }

            return text;
        }

        public static bool IsSpeakable(string text)
        {
            // TextToTalk#41 Unspeakable text
            return Speakable.Match(text).Success;
        }

        public static string GetPlayerNameWithoutWorld(SeString playerName)
        {
            if (playerName.Payloads.FirstOrDefault(p => p is PlayerPayload) is PlayerPayload player)
            {
                return player.PlayerName;
            }

            return playerName.TextValue;
        }

        public static string? GetPartialName(string? name, FirstOrLastName part)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var names = name.Split(' ');
            return part switch
            {
                FirstOrLastName.First => names[0],
                FirstOrLastName.Last => names.Length == 1 ? names[0] : names[^1], // Some NPCs only have one name.
                _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Enumeration value is out of range."),
            };
        }

        public static string ExtractTokens(string text, IReadOnlyDictionary<string, string?> tokenMap)
        {
            // Extract tokens from the longest target values down to the shortest, to e.g.
            // extract full names before first and last names.
            foreach (var (k, v) in tokenMap
                         .Where(kvp => kvp.Value is not null)
                         .OrderByDescending(kvp => kvp.Value?.Length))
            {
                text = text.Replace(v!, k);
            }

            return text;
        }
    }
}