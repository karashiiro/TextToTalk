using System.Collections.Generic;

namespace TextToTalk
{
    public class EnabledChatTypesPreset
    {
        public bool EnableAllChatTypes { get; set; }

        public IList<int> EnabledChatTypes { get; set; }
    }
}