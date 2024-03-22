using System.Collections.Generic;
using System.Linq;
using R3;
using TextToTalk.Events;

namespace TextToTalk;

public partial class TextToTalk
{
    private Observable<ChatTextEmitEvent> OnChatTextEmit()
    {
        return Observable.FromEvent<ChatTextEmitEvent>(
                h => this.chatMessageHandler.OnTextEmit += h,
                h => this.chatMessageHandler.OnTextEmit -= h)
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

    private Observable<TextEmitEvent> OnTalkAddonTextEmit()
    {
        return Observable.FromEvent<TextEmitEvent>(
            h => this.addonTalkHandler.OnTextEmit += h,
            h => this.addonTalkHandler.OnTextEmit -= h);
    }

    private Observable<TextEmitEvent> OnBattleTalkAddonTextEmit()
    {
        return Observable.FromEvent<TextEmitEvent>(
            h => this.addonBattleTalkHandler.OnTextEmit += h,
            h => this.addonBattleTalkHandler.OnTextEmit -= h);
    }

    private Observable<AddonTalkAdvanceEvent> OnTalkAddonAdvance()
    {
        return Observable.FromEvent<AddonTalkAdvanceEvent>(
            h => this.addonTalkHandler.OnAdvance += h,
            h => this.addonTalkHandler.OnAdvance -= h);
    }

    private Observable<AddonTalkCloseEvent> OnTalkAddonClose()
    {
        return Observable.FromEvent<AddonTalkCloseEvent>(
            h => this.addonTalkHandler.OnClose += h,
            h => this.addonTalkHandler.OnClose -= h);
    }

    private Observable<SourcedTextEvent> OnTextSourceCancel()
    {
        return OnTalkAddonAdvance()
            .Cast<AddonTalkAdvanceEvent, SourcedTextEvent>()
            .Merge(OnTalkAddonClose().Cast<AddonTalkCloseEvent, SourcedTextEvent>());
    }

    private Observable<TextEmitEvent> OnTextEmit()
    {
        return OnTalkAddonTextEmit()
            .Merge(OnBattleTalkAddonTextEmit())
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