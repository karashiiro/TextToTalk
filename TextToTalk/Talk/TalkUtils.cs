using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TextToTalk.Talk
{
    public class TalkUtils
    {
        public static unsafe TalkAddonText ReadTalkAddon(AddonTalk* talkAddon)
        {
            return new()
            {
                Speaker = ReadTextNode(talkAddon->AtkTextNode220),
                Text = ReadTextNode(talkAddon->AtkTextNode228),
            };
        }

        private static unsafe string ReadTextNode(AtkTextNode* textNode)
        {
            var textPtr = textNode->NodeText.StringPtr;
            var textLength = textNode->NodeText.BufUsed - 1; // Null-terminated; chop off the null byte
            if (textLength <= 0) return "";

            var text = Encoding.UTF8.GetString(textPtr, (int)textLength)
                .Replace("????", ""); // Newlines are weird - this removes them after they've already been unsuccessfully parsed
            return text;
        }
    }
}