using System.Collections.Generic;
using System.Linq;
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
        
        public bool EnableAllChatTypes { get; set; }
        public IList<int> EnabledChatTypes { get; set; }
        public IList<Trigger> Bad { get; set; }
        public IList<Trigger> Good { get; set; }

        public int Rate { get; set; }
        public int Volume { get; set; }
        public int GenderIndex { get; set; }
        public int AgeIndex { get; set; }

        public bool UseWebsocket { get; set; }

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public PluginConfiguration()
        {
            Enabled = true;

            EnabledChatTypes = new List<int>
            {
                (int) XivChatType.Say,
                (int) XivChatType.Shout,
                (int) XivChatType.Party,
                (int) AdditionalChatTypes.Enum.BeneficialEffectOnYou,
                (int) AdditionalChatTypes.Enum.BeneficialEffectOnYouEnded,
                (int) AdditionalChatTypes.Enum.BeneficialEffectOnOtherPlayer,
            };
            Bad = new List<Trigger>();
            Good = new List<Trigger>();

            using var ss = new SpeechSynthesizer();
            Rate = ss.Rate;
            Volume = ss.Volume;
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            EnabledChatTypes = EnabledChatTypes.Distinct().ToList();
        }

        public void Save()
        {
            EnabledChatTypes = EnabledChatTypes.Distinct().ToList();
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
