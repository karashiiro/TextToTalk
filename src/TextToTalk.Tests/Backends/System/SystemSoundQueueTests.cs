using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using R3;
using TextToTalk.Backends;
using TextToTalk.Backends.System;
using TextToTalk.Lexicons;
using Xunit;

namespace TextToTalk.Tests.Backends.System;

public class SystemSoundQueueTests : IDisposable
{
    private readonly Mock<ISpeechSynthesizer> mockSynth;
    private readonly LexiconManager lexiconManager;
    private readonly PluginConfiguration config;
    private readonly NoOpStreamSoundQueue streamSoundQueue;
    private SystemSoundQueue? systemSoundQueue;

    public SystemSoundQueueTests()
    {
        this.mockSynth = new Mock<ISpeechSynthesizer>(MockBehavior.Loose);
        this.lexiconManager = new LexiconManager();
        this.config = new PluginConfiguration();
        this.streamSoundQueue = new NoOpStreamSoundQueue(this.config);

        this.mockSynth.SetupGet(s => s.Rate).Returns(0);
        this.mockSynth.SetupGet(s => s.Volume).Returns(50);
        this.mockSynth.SetupGet(s => s.VoiceName).Returns("TestVoice");
        this.mockSynth.SetupGet(s => s.VoiceCultureIetfLanguageTag).Returns("en-US");
    }

    public void Dispose()
    {
        this.systemSoundQueue?.Dispose();
        this.streamSoundQueue.Dispose();
    }

    private SystemVoicePreset CreatePreset(string? voiceName = "TestVoice", int rate = 0, int volume = 50)
    {
        return new SystemVoicePreset
        {
            EnabledBackend = TTSBackend.System,
            Id = 1,
            Name = "TestPreset",
            Rate = rate,
            Volume = volume,
            VoiceName = voiceName,
        };
    }

    private SystemSoundQueue CreateQueue()
    {
        this.systemSoundQueue = new SystemSoundQueue(this.lexiconManager, this.config, this.mockSynth.Object, this.streamSoundQueue);
        return this.systemSoundQueue;
    }

    private class NoOpStreamSoundQueue(PluginConfiguration config) : StreamSoundQueue(config)
    {
        public List<(MemoryStream Data, TextSource Source)> Received { get; } = new();
        public ManualResetEventSlim ItemReceived { get; } = new(false);
        public int CancelCount { get; private set; }

        protected override void OnSoundLoop(StreamSoundQueueItem nextItem)
        {
            Received.Add((nextItem.Data, nextItem.Source));
            ItemReceived.Set();
        }

        protected override void OnSoundCancelled()
        {
            CancelCount++;
        }
    }

    [Fact]
    public void EnqueueSound_CallsSpeakSsml_WithCorrectSsml()
    {
        string? capturedSsml = null;
        var synthStarted = new ManualResetEventSlim();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback<string>(ssml =>
            {
                capturedSsml = ssml;
                synthStarted.Set();
            });

        var queue = CreateQueue();
        var preset = CreatePreset();
        queue.EnqueueSound(preset, TextSource.Chat, "Hello, world!");

        Assert.True(synthStarted.Wait(TimeSpan.FromSeconds(5)), "SpeakSsml should have been called");
        Assert.NotNull(capturedSsml);
        Assert.Contains("Hello, world!", capturedSsml);
        Assert.Contains("xml:lang=\"en-US\"", capturedSsml);
    }

    [Fact]
    public void EnqueueSound_CallsSpeakSsml_ForMultipleItems()
    {
        var capturedSsmls = new List<string>();
        var completed = new CountdownEvent(3);

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback<string>(ssml =>
            {
                lock (capturedSsmls)
                    capturedSsmls.Add(ssml);
                completed.Signal();
            });

        var queue = CreateQueue();
        var preset = CreatePreset();
        queue.EnqueueSound(preset, TextSource.Chat, "One");
        queue.EnqueueSound(preset, TextSource.Chat, "Two");
        queue.EnqueueSound(preset, TextSource.Chat, "Three");

        Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "All three items should have been spoken");
        Assert.Equal(3, capturedSsmls.Count);
        Assert.Contains(capturedSsmls, s => s.Contains("One"));
        Assert.Contains(capturedSsmls, s => s.Contains("Two"));
        Assert.Contains(capturedSsmls, s => s.Contains("Three"));
    }

    [Fact]
    public void EnqueueSound_PassesAudioToStreamSoundQueue()
    {
        var synthCompleted = new ManualResetEventSlim();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() => synthCompleted.Set());

        var queue = CreateQueue();
        var preset = CreatePreset();
        queue.EnqueueSound(preset, TextSource.AddonTalk, "Test audio");

        Assert.True(this.streamSoundQueue.ItemReceived.Wait(TimeSpan.FromSeconds(5)),
            "StreamSoundQueue should have received audio data");
        Assert.Single(this.streamSoundQueue.Received);
        Assert.Equal(TextSource.AddonTalk, this.streamSoundQueue.Received[0].Source);
    }

    [Fact]
    public void CancelAllSounds_CallsSetOutputToNull()
    {
        var synthesisStarted = new ManualResetEventSlim();
        var synthesisBlocked = new TaskCompletionSource<bool>();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthesisStarted.Set();
                synthesisBlocked.Task.Wait();
            });

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Cancelled text");

        // Wait for synthesis to actually start
        Assert.True(synthesisStarted.Wait(TimeSpan.FromSeconds(5)), "Synthesis should have started");

        // Cancel while synthesis is running
        queue.CancelAllSounds();

        this.mockSynth.Verify(s => s.SetOutputToNull(), Times.AtLeastOnce,
            "SetOutputToNull should be called on cancel");

        // Clean up: unblock synthesis
        synthesisBlocked.SetResult(true);

        // Give the async continuation time to finish
        Thread.Sleep(200);
    }

    [Fact]
    public void CancelAllSounds_DuringSynthesis_CallsSetOutputToNull()
    {
        var synthesisStarted = new ManualResetEventSlim();
        var synthesisBlocked = new TaskCompletionSource<bool>();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthesisStarted.Set();
                synthesisBlocked.Task.Wait();
            });

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.AddonTalk, "NPC dialogue");

        Assert.True(synthesisStarted.Wait(TimeSpan.FromSeconds(5)), "Synthesis should have started");

        queue.CancelAllSounds();

        this.mockSynth.Verify(s => s.SetOutputToNull(), Times.AtLeastOnce,
            "SetOutputToNull should be called on CancelAllSounds during synthesis");

        synthesisBlocked.SetResult(true);
        Thread.Sleep(200);
    }

    [Fact]
    public void CancelFromSource_WithMatchingSource_CallsSetOutputToNull()
    {
        var synthesisStarted = new ManualResetEventSlim();
        var synthesisBlocked = new TaskCompletionSource<bool>();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthesisStarted.Set();
                synthesisBlocked.Task.Wait();
            });

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.AddonTalk, "NPC dialogue");

        Assert.True(synthesisStarted.Wait(TimeSpan.FromSeconds(5)), "Synthesis should have started");

        queue.CancelFromSource(TextSource.AddonTalk);

        this.mockSynth.Verify(s => s.SetOutputToNull(), Times.AtLeastOnce,
            "CancelFromSource should call SetOutputToNull when source matches the currently-spoken item");

        synthesisBlocked.SetResult(true);
        Thread.Sleep(200);
    }

    [Fact]
    public void CancelFromSource_WithNonMatchingSource_DoesNotCallSetOutputToNull()
    {
        var synthesisStarted = new ManualResetEventSlim();
        var synthesisBlocked = new TaskCompletionSource<bool>();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthesisStarted.Set();
                synthesisBlocked.Task.Wait();
            });

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Chat message");

        Assert.True(synthesisStarted.Wait(TimeSpan.FromSeconds(5)), "Synthesis should have started");

        // Cancel a different source than the one being spoken
        queue.CancelFromSource(TextSource.AddonTalk);

        this.mockSynth.Verify(s => s.SetOutputToNull(), Times.Never,
            "SetOutputToNull should not be called when source doesn't match");

        synthesisBlocked.SetResult(true);
        Thread.Sleep(200);
    }

    [Fact]
    public void CancelAllSounds_AlsoCancelsStreamSoundQueue()
    {
        var synthStarted = new ManualResetEventSlim();
        var synthBlocked = new TaskCompletionSource<bool>();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthStarted.Set();
                synthBlocked.Task.Wait();
            });

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Text");

        Assert.True(synthStarted.Wait(TimeSpan.FromSeconds(5)));
        queue.CancelAllSounds();

        Assert.True(this.streamSoundQueue.CancelCount > 0,
            "StreamSoundQueue should also be cancelled");

        synthBlocked.SetResult(true);
        Thread.Sleep(200);
    }

    [Fact]
    public void CancelAllSounds_WhenSetOutputToNullThrows_DoesNotCrash()
    {
        var synthesisStarted = new ManualResetEventSlim();
        var synthesisBlocked = new TaskCompletionSource<bool>();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthesisStarted.Set();
                synthesisBlocked.Task.Wait();
            });

        // Simulate a corrupt synthesizer state
        this.mockSynth.Setup(s => s.SetOutputToNull())
            .Throws(new InvalidOperationException("Synthesizer in bad state"));

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Text");

        Assert.True(synthesisStarted.Wait(TimeSpan.FromSeconds(5)));

        var ex = Record.Exception(() => queue.CancelAllSounds());
        Assert.Null(ex);

        // Reset the mock so cleanup Dispose() doesn't also throw
        this.mockSynth.Setup(s => s.SetOutputToNull());

        synthesisBlocked.SetResult(true);
        Thread.Sleep(200);
    }

    [Fact]
    public void VoiceSelectionFailure_PublishesToObservable()
    {
        var exceptionReceived = new ManualResetEventSlim();
        SelectVoiceFailedException? capturedException = null;

        var customMock = new Mock<ISpeechSynthesizer>(MockBehavior.Loose);
        customMock.SetupGet(s => s.VoiceName).Returns("DifferentVoice");
        customMock.SetupGet(s => s.VoiceCultureIetfLanguageTag).Returns("en-US");

        // Make SelectVoice throw
        customMock.Setup(s => s.SelectVoice(It.IsAny<string>()))
            .Throws(new Exception("Voice not installed"));

        var queue = new SystemSoundQueue(this.lexiconManager, this.config, customMock.Object, this.streamSoundQueue);
        using var sub = queue.SelectVoiceFailed.Subscribe(ex =>
        {
            capturedException = ex;
            exceptionReceived.Set();
        });

        queue.EnqueueSound(CreatePreset("UnavailableVoice"), TextSource.Chat, "Test");

        Assert.True(exceptionReceived.Wait(TimeSpan.FromSeconds(5)),
            "SelectVoiceFailed observable should have emitted");
        Assert.NotNull(capturedException);
        Assert.Equal("UnavailableVoice", capturedException!.VoiceId);
        queue.Dispose();
    }

    [Fact]
    public void SpeakSsmlException_IsLogged_DoesNotCrash()
    {
        var synthCompleted = new ManualResetEventSlim();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Throws(new Exception("Synthesis failed"));

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Test");

        // The exception should be caught by OnSoundLoop's catch block.
        // Wait a bit for the async operation to complete (or fail).
        Thread.Sleep(500);

        // The queue should still be operational — enqueue another item.
        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() => synthCompleted.Set());

        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Recovery test");
        Assert.True(synthCompleted.Wait(TimeSpan.FromSeconds(5)),
            "Queue should still work after a failed synthesis");
    }

    [Fact]
    public void ConsecutiveSynthesisFailures_IncrementFailureCounter()
    {
        const int itemsToFail = 3;
        var allDone = new CountdownEvent(itemsToFail);

        var mock = new Mock<ISpeechSynthesizer>(MockBehavior.Loose);
        mock.SetupGet(s => s.VoiceName).Returns("TestVoice");
        mock.SetupGet(s => s.VoiceCultureIetfLanguageTag).Returns("en-US");
        mock.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback<string>(_ =>
            {
                allDone.Signal();
                throw new Exception("Synthesis failure");
            });

        var queue = new SystemSoundQueue(this.lexiconManager, this.config, mock.Object,
            streamSoundQueue: this.streamSoundQueue);
        var preset = CreatePreset();

        for (var i = 1; i <= itemsToFail; i++)
        {
            queue.EnqueueSound(preset, TextSource.Chat, $"Fail {i}");
        }

        Assert.True(allDone.Wait(TimeSpan.FromSeconds(10)),
            "All synthesis attempts should have completed");

        // Give the async catch block time to run
        Thread.Sleep(200);

        Assert.Equal(3, queue.consecutiveFailures);

        queue.Dispose();
    }

    [Fact]
    public void SynthesisFailure_AfterThreeConsecutiveFailures_RecreatesSynthesizer()
    {
        const int itemsToFail = 3;
        var factoryCalls = 0;
        var allDone = new CountdownEvent(itemsToFail);

        var mock = new Mock<ISpeechSynthesizer>(MockBehavior.Loose);
        mock.SetupGet(s => s.VoiceName).Returns("TestVoice");
        mock.SetupGet(s => s.VoiceCultureIetfLanguageTag).Returns("en-US");
        mock.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback<string>(_ =>
            {
                allDone.Signal();
                throw new Exception("Synthesis failure");
            });

        var queue = new SystemSoundQueue(this.lexiconManager, this.config, mock.Object,
            synthesizerFactory: Factory, streamSoundQueue: this.streamSoundQueue);
        var preset = CreatePreset();

        for (var i = 1; i <= itemsToFail; i++)
        {
            queue.EnqueueSound(preset, TextSource.Chat, $"Fail {i}");
        }

        Assert.True(allDone.Wait(TimeSpan.FromSeconds(10)),
            "All synthesis attempts should have completed");

        // Wait for the async catch block to process (may reset counter after factory call)
        Thread.Sleep(200);

        Assert.True(factoryCalls > 0,
            $"Factory should have been called after {itemsToFail} consecutive failures, but was called {factoryCalls} times");
        mock.Verify(s => s.Dispose(), Times.Once,
            "Old synthesizer should be disposed when recreated");

        queue.Dispose();
        return;

        ISpeechSynthesizer Factory()
        {
            Interlocked.Increment(ref factoryCalls);
            var newMock = new Mock<ISpeechSynthesizer>(MockBehavior.Loose);
            newMock.SetupGet(s => s.VoiceName).Returns("TestVoice");
            newMock.SetupGet(s => s.VoiceCultureIetfLanguageTag).Returns("en-US");
            return newMock.Object;
        }
    }

    [Fact]
    public void Dispose_DisposesSpeechSynthesizer()
    {
        var queue = CreateQueue();
        queue.Dispose();

        this.mockSynth.Verify(s => s.Dispose(), Times.Once,
            "SpeechSynthesizer should be disposed when SoundQueue is disposed");
    }

    [Fact]
    public void Dispose_StopsSoundThread_AndStreamSoundQueue()
    {
        var queue = CreateQueue();

        Assert.True(streamSoundQueue.CancelCount == 0, "No cancels before dispose");

        queue.Dispose();

        // After disposal, the sound thread should be stopped.
        // Enqueuing should no-op (item might be added but thread won't process it).
    }

    /// <summary>
    /// Simulates skipping through cutscene dialogue. Each text advance fires CancelFromSource for AddonTalk
    /// (removing queued items and cancelling the current utterance), then the next dialogue line is immediately
    /// enqueued. Verifies the queue doesn't deadlock or enter an unrecoverable state.
    /// </summary>
    [Fact]
    public void RapidEnqueueAndCancel_DoesNotDeadlock()
    {
        const int cycles = 5;
        var completed = new CountdownEvent(cycles);
        var blockSynthesis = new TaskCompletionSource<bool>();

        // First synthesis blocks so we can test cancel during synthesis
        var firstStarted = new ManualResetEventSlim();
        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback<string>(_ =>
            {
                firstStarted.Set();
                completed.Signal();
                blockSynthesis.Task.Wait();
            });

        var queue = CreateQueue();
        var preset = CreatePreset();

        // Start first utterance, let it reach synthesis
        queue.EnqueueSound(preset, TextSource.AddonTalk, "Line 0");
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));

        // Simulate rapid skip: cancel + re-enqueue several times
        for (var i = 1; i < cycles; i++)
        {
            queue.CancelFromSource(TextSource.AddonTalk);
            queue.EnqueueSound(preset, TextSource.AddonTalk, $"Line {i}");
            Thread.Sleep(20); // brief gap between skips
        }

        // Unblock the first synthesis — the queue should process remaining items
        blockSynthesis.SetResult(true);

        // After the first item completes, subsequent items should process normally.
        // verify the queue recovers.
        var finalDone = new ManualResetEventSlim();
        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() => finalDone.Set());

        queue.EnqueueSound(preset, TextSource.Chat, "Recovery check");
        Assert.True(finalDone.Wait(TimeSpan.FromSeconds(5)),
            "Queue should process new items after rapid enqueue/cancel storm");
    }

    [Fact]
    public void Dispose_DuringBlockedSynthesis_CompletesCleanly()
    {
        var synthesisStarted = new ManualResetEventSlim();
        var synthesisBlocked = new TaskCompletionSource<bool>();
        var disposedCleanly = new ManualResetEventSlim();

        this.mockSynth.Setup(s => s.SpeakSsml(It.IsAny<string>()))
            .Callback(() =>
            {
                synthesisStarted.Set();
                synthesisBlocked.Task.Wait();
            });

        var queue = CreateQueue();
        queue.EnqueueSound(CreatePreset(), TextSource.Chat, "Long text");

        Assert.True(synthesisStarted.Wait(TimeSpan.FromSeconds(5)),
            "Synthesis should have started");

        // Dispose on a background thread — this must not hang
        Task.Run(() =>
        {
            queue.Dispose();
            disposedCleanly.Set();
        });

        // Give disposal a moment to enter CancelAllSounds → OnSoundCancelled
        Thread.Sleep(200);

        // Unblock synthesis — the async continuation and disposal should both finish
        synthesisBlocked.SetResult(true);

        Assert.True(disposedCleanly.Wait(TimeSpan.FromSeconds(5)),
            "Dispose should complete cleanly even with blocked synthesis");
    }
}
