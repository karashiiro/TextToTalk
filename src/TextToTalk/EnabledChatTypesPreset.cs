using System.Collections.Generic;

namespace TextToTalk
{
    public class EnabledChatTypesPreset
    {
        public int Id { get; set; }

        public bool EnableAllChatTypes { get; set; }

        public IList<int> EnabledChatTypes { get; set; }

        public string Name { get; set; }

        public bool UseKeybind { get; set; }

        public VirtualKey.Enum ModifierKey { get; set; }

        public VirtualKey.Enum MajorKey { get; set; }
    }
}