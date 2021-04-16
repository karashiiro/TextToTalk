using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace TextToTalk
{
    public class PluginConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool Enabled { get; set; }

        public bool FirstTime { get; set; }
        
        public bool EnableAllChatTypes { get; set; }
        public IList<int> EnabledChatTypes { get; set; }
        public IList<Trigger> Bad { get; set; }
        public IList<Trigger> Good { get; set; }

        public int Rate { get; set; }
        public int Volume { get; set; }

        public string VoiceName { get; set; }

        public bool UseWebsocket { get; set; }

        public bool NameNpcWithSay { get; set; } = true;

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public PluginConfiguration()
        {
            Enabled = true;

            Bad = new List<Trigger>();
            Good = new List<Trigger>();

            using var ss = new SpeechSynthesizer();
            Rate = ss.Rate;
            Volume = ss.Volume;

            VoiceName = ss.GetInstalledVoices().First().VoiceInfo.Name;
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            if (!FirstTime)
            {
                EnabledChatTypes = new List<int>
                {
                    (int) XivChatType.Say,
                    (int) XivChatType.Shout,
                    (int) XivChatType.Party,
                    (int) AdditionalChatTypes.Enum.BeneficialEffectOnYou,
                    (int) AdditionalChatTypes.Enum.BeneficialEffectOnYouEnded,
                    (int) AdditionalChatTypes.Enum.BeneficialEffectOnOtherPlayer,
                };
                FirstTime = true;
            }

            this.pluginInterface.SavePluginConfig(this);
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
