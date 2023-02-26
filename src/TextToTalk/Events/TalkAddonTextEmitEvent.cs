using Dalamud.Game.ClientState.Objects.Types;

namespace TextToTalk.Events;

public class TalkAddonTextEmitEvent : TextEmitEvent
{
    public GameObject? Object { get; }

    public TalkAddonTextEmitEvent(
        TextSource source,
        string? speaker,
        string? text,
        GameObject? gameObject) : base(source, speaker ?? "", text ?? "")
    {
        Object = gameObject;
    }
}