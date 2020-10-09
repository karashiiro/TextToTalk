using System.Collections.Generic;
using System.Speech.Synthesis;
using Dalamud.Configuration;
using Dalamud.Game.Chat;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace TextToTalk
{
    public class PluginConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool Enabled { get; set; }
        
        public IList<XivChatType> DisabledChatTypes { get; set; }
        public IList<string> Bad { get; set; }
        public IList<string> Good { get; set; }

        public int Rate { get; set; }
        public int Volume { get; set; }
        public int GenderIndex { get; set; }
        public int AgeIndex { get; set; }

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public PluginConfiguration()
        {
            Enabled = true;

            DisabledChatTypes = new List<XivChatType>();
            Bad = new List<string>();
            Good = new List<string>();

            using var ss = new SpeechSynthesizer();
            Rate = ss.Rate;
            Volume = ss.Volume;
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
