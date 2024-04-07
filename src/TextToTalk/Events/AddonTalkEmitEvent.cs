using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;

namespace TextToTalk.Events;

public class AddonTalkEmitEvent(SeString speaker, SeString text, GameObject? speakerObj, string? voiceFile)
    : TextEmitEvent(TextSource.AddonTalk, speaker, text, speakerObj, voiceFile);