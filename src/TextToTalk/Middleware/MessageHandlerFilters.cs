using Dalamud.Game.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.Middleware;

public class MessageHandlerFilters
{
    private readonly PluginConfiguration config;
    private readonly SharedState sharedState;

    public MessageHandlerFilters(SharedState sharedState, PluginConfiguration config)
    {
        this.sharedState = sharedState;
        this.config = config;
    }

    public bool IsDuplicateQuestText(string text)
    {
        return this.sharedState.LastQuestText == text;
    }

    public void SetLastQuestText(string text)
    {
        this.sharedState.LastQuestText = text;
    }

    public bool IsSameSpeaker(string speaker)
    {
        return this.sharedState.LastSpeaker == speaker;
    }

    public void SetLastSpeaker(string speaker)
    {
        this.sharedState.LastSpeaker = speaker;
    }

    public bool ShouldSaySender()
    {
        return this.config.EnableNameWithSay && this.config.NameNpcWithSay;
    }

    public bool ShouldSaySender(XivChatType type)
    {
        return this.config.EnableNameWithSay && (this.config.NameNpcWithSay || (int)type != (int)AdditionalChatType.NPCDialogue);
    }
}