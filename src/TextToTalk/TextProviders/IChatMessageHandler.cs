using System;
using R3;
using TextToTalk.Events;

namespace TextToTalk.TextProviders;

public interface IChatMessageHandler : IDisposable
{
    Observable<ChatTextEmitEvent> OnTextEmit();
}