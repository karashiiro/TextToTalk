using System.Collections.Generic;
using System.Linq;
using R3;
using TextToTalk.Events;

namespace TextToTalk;

public partial class TextToTalk
{
    private Observable<ChatTextEmitEvent> OnChatTextEmit()
    {
        return this.chatMessageHandler.OnTextEmit()
            .Where(this, static (ev, p) =>
            {
                // Check all of the other filters to see if this should be dropped
                var chatTypes = p.config.GetCurrentEnabledChatTypesPreset();
                var typeEnabled = chatTypes.EnabledChatTypes is not null &&
                                  chatTypes.EnabledChatTypes.Contains((int)ev.ChatType);
                return chatTypes.EnableAllChatTypes || typeEnabled;
            })
            .Where(this, static (ev, p) => !p.IsTextBad(ev.Text.TextValue))
            .Where(this, static (ev, p) => p.IsTextGood(ev.Text.TextValue));
    }

    private Observable<SourcedTextEvent> OnTextSourceCancel()
    {
        return this.addonTalkHandler.OnAdvance()
            .Cast<AddonTalkAdvanceEvent, SourcedTextEvent>()
            .Merge(this.addonTalkHandler.OnClose().Cast<AddonTalkCloseEvent, SourcedTextEvent>());
    }

    private Observable<TextEmitEvent> OnTextEmit()
    {
        return this.addonTalkHandler.OnTextEmit()
            .Merge(this.addonBattleTalkHandler.OnTextEmit())
            .Merge(OnChatTextEmit().Cast<ChatTextEmitEvent, TextEmitEvent>())
            .DistinctUntilChanged(EqualityComparer<TextEmitEvent>.Create((a, b) => a?.IsEquivalent(b) ?? a == b));
    }

    private bool IsTextGood(string text)
    {
        if (!this.config.Good.Any())
        {
            return true;
        }

        return this.config.Good
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Any(t => t.Match(text));
    }

    private bool IsTextBad(string text)
    {
        return this.config.Bad
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Any(t => t.Match(text));
    }
}