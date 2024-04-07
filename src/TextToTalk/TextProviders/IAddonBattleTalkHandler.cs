using System;
using R3;
using TextToTalk.Events;

namespace TextToTalk.TextProviders;

public interface IAddonBattleTalkHandler : IDisposable
{
    Observable<TextEmitEvent> OnTextEmit();

    void PollAddon(AddonPollSource pollSource, string? voiceFile);
}