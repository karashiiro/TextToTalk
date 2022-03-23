using Dalamud.Game.Text;
using TextToTalk.GameEnums;

namespace TextToTalk.Middleware;

public class MessageHandlerFilters
{
    private readonly PluginConfiguration config;
    private readonly Services services;

    public MessageHandlerFilters(Services services, PluginConfiguration config)
    {
        this.services = services;
        this.config = config;
    }

    public bool IsDuplicateQuestText(string text)
    {
        var sharedState = this.services.GetService<SharedState>();
        return sharedState.LastQuestText == text;
    }

    public void SetLastQuestText(string text)
    {
        var sharedState = this.services.GetService<SharedState>();
        sharedState.LastQuestText = text;
    }

    public bool IsSameSpeaker(string speaker)
    {
        var sharedState = this.services.GetService<SharedState>();
        return sharedState.LastSpeaker == speaker;
    }

    public void SetLastSpeaker(string speaker)
    {
        var sharedState = this.services.GetService<SharedState>();
        sharedState.LastSpeaker = speaker;
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