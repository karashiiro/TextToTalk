using System;
using R3;
using TextToTalk.Events;

namespace TextToTalk.TextProviders;

public interface IAddonTalkHandler : IDisposable
{
    Observable<TextEmitEvent> OnTextEmit();

    Observable<AddonTalkAdvanceEvent> OnAdvance();

    Observable<AddonTalkCloseEvent> OnClose();

    void PollAddon(AddonPollSource pollSource);
}