using System;

namespace TextToTalk;

public class DisposeHandler : IDisposable
{
    private readonly Action dispose;

    public DisposeHandler(Action dispose)
    {
        this.dispose = dispose;
    }

    public void Dispose()
    {
        this.dispose();
    }
}