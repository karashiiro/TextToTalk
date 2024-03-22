using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class AddonBattleTalkEmitEvent(SeString speaker, SeString text, GameObject? speakerObj)
    : TextEmitEvent(TextSource.AddonBattleTalk, speaker, text, speakerObj);