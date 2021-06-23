using Dalamud.CrystalTower.UI;

namespace TextToTalk.UI
{
    public class PresetModificationWindow : ImmediateModeWindow
    {
        public PluginConfiguration Configuration { get; set; }

        public int PresetId { get; set; }

        public override void Draw(ref bool visible)
        {
        }
    }
}