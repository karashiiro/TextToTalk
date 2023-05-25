using System;
using System.Linq;
using System.Reactive.Linq;
using TextToTalk.Events;

namespace TextToTalk;

public partial class TextToTalk
{
    private IObservable<ChatTextEmitEvent> OnChatTextEmit()
    {
        return Observable.FromEvent<ChatTextEmitEvent>(
                h => this.chatMessageHandler.OnTextEmit += h,
                h => this.chatMessageHandler.OnTextEmit -= h)
            .Where(ev =>
            {
                // Check all of the other filters to see if this should be dropped
                var chatTypes = this.config.GetCurrentEnabledChatTypesPreset();
                var typeEnabled = chatTypes.EnabledChatTypes is not null &&
                                  chatTypes.EnabledChatTypes.Contains((int)ev.ChatType);
                return chatTypes.EnableAllChatTypes || typeEnabled;
            })
            .Where(ev => !IsTextBad(ev.Text.TextValue))
            .Where(ev => IsTextGood(ev.Text.TextValue));
    }

    private IObservable<TextEmitEvent> OnTalkAddonTextEmit()
    {
        return Observable.FromEvent<TextEmitEvent>(
            h => this.addonTalkHandler.OnTextEmit += h,
            h => this.addonTalkHandler.OnTextEmit -= h);
    }

    private IObservable<TextEmitEvent> OnBattleTalkAddonTextEmit()
    {
        return Observable.FromEvent<TextEmitEvent>(
            h => this.addonBattleTalkHandler.OnTextEmit += h,
            h => this.addonBattleTalkHandler.OnTextEmit -= h);
    }

    private IObservable<AddonTalkAdvanceEvent> OnTalkAddonAdvance()
    {
        return Observable.FromEvent<AddonTalkAdvanceEvent>(
            h => this.addonTalkHandler.OnAdvance += h,
            h => this.addonTalkHandler.OnAdvance -= h);
    }

    private IObservable<AddonTalkCloseEvent> OnTalkAddonClose()
    {
        return Observable.FromEvent<AddonTalkCloseEvent>(
            h => this.addonTalkHandler.OnClose += h,
            h => this.addonTalkHandler.OnClose -= h);
    }

    private IObservable<SourcedTextEvent> OnTextSourceCancel()
    {
        return OnTalkAddonAdvance().Merge<SourcedTextEvent>(OnTalkAddonClose());
    }

    private IObservable<TextEmitEvent> OnTextEmit()
    {
        return OnTalkAddonTextEmit().Merge(OnChatTextEmit()).Merge(OnBattleTalkAddonTextEmit());
    }

    private bool IsTextGood(string text)
    {
        if (!this.config.Good.Any())
        {
            return true;
        }

        return this.config.Good
            .Where(t => t.Text != "")
            .Any(t => t.Match(text));
    }

    private bool IsTextBad(string text)
    {
        return this.config.Bad
            .Where(t => t.Text != "")
            .Any(t => t.Match(text));
    }
}