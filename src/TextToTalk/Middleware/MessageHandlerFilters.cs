using Dalamud.Game.ClientState;
using Dalamud.Game.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.Middleware;

public class MessageHandlerFilters
{
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;
    private readonly ClientState clientState;

    public MessageHandlerFilters(SharedState sharedState, PluginConfiguration config, ClientState clientState)
    {
        this.sharedState = sharedState;
        this.config = config;
        this.clientState = clientState;
    }

    public bool IsSameSpeaker(string? speaker)
    {
        return this.sharedState.LastSpeaker == speaker;
    }

    public void SetLastSpeaker(string? speaker)
    {
        this.sharedState.LastSpeaker = speaker;
    }

    public bool ShouldSaySender()
    {
        return this.config is { EnableNameWithSay: true, NameNpcWithSay: true };
    }

    public bool ShouldProcessSpeaker(string? speaker)
    {
        if (!string.IsNullOrEmpty(speaker) && ShouldSaySender())
        {
            // Only if we allow the speaker's name to be repeated each time they speak,
            // or the speaker has actually changed.
            if (!this.config.DisallowMultipleSay || !IsSameSpeaker(speaker))
            {
                return true;
            }
        }

        return false;
    }

    public bool ShouldSaySender(XivChatType type)
    {
        return this.config.EnableNameWithSay &&
               (this.config.NameNpcWithSay || (int)type != (int)AdditionalChatType.NPCDialogue);
    }

    public bool ShouldSayFromYou(string? speaker)
    {
        if (string.IsNullOrEmpty(speaker))
        {
            return true;
        }

        return !this.config.SkipMessagesFromYou ||
               !speaker.Contains(this.clientState.LocalPlayer?.Name.TextValue ?? "");
    }
}