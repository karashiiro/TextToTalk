using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Moq;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using TextToTalk.Backends.Websocket;
using TextToTalk.Events;
using TextToTalk.Services;
using TextToTalk.Talk;
using Xunit;

namespace TextToTalk.Tests.Services;

public class DialogueSessionServiceTests
{
    private readonly Mock<IFramework> framework;
    private readonly Mock<IClientState> clientState;
    private readonly Mock<ICondition> condition;
    private readonly Mock<IAddonTalkManager> talk;
    private readonly Mock<IAddonBattleTalkManager> battleTalk;
    private readonly Mock<IAddonSelectStringManager> selectString;
    private readonly Mock<IAddonSelectIconStringManager> selectIcon;

    public DialogueSessionServiceTests()
    {
        framework = new Mock<IFramework>();
        clientState = new Mock<IClientState>();
        condition = new Mock<ICondition>();
        talk = new Mock<IAddonTalkManager>();
        battleTalk = new Mock<IAddonBattleTalkManager>();
        selectString = new Mock<IAddonSelectStringManager>();
        selectIcon = new Mock<IAddonSelectIconStringManager>();

        framework.Setup(f => f.Run(It.IsAny<Action>(), It.IsAny<CancellationToken>()))
            .Callback<Action, CancellationToken>((action, _) => action());
    }

    private DialogueSessionService CreateService() => new(
        framework.Object, clientState.Object, condition.Object,
        selectString.Object, selectIcon.Object,
        talk.Object, battleTalk.Object);

    private void AdvanceFrame() =>
        framework.Raise(f => f.Update += null, framework.Object);

    // ---------------------------------------------------------------
    // Session start
    // ---------------------------------------------------------------

    [Fact]
    public void NpcText_StartsSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        Assert.Single(events);
        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(TextSource.AddonTalk, started.Source);
        Assert.Equal(DialogueEventReason.TextReceived, started.Reason);
    }

    [Fact]
    public void BattleDialogue_StartsSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonBattleTalk);

        Assert.Single(events);
        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(TextSource.AddonBattleTalk, started.Source);
        Assert.Equal(DialogueEventReason.TextReceived, started.Reason);
    }

    [Fact]
    public void DialogueAddonVisibility_StartsSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        talk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();

        Assert.Single(events);
        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(TextSource.AddonTalk, started.Source);
        Assert.Equal(DialogueEventReason.AddonShown, started.Reason);
    }

    [Fact]
    public void BattleTalkAddonVisibility_StartsSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        battleTalk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();

        Assert.Single(events);
        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(TextSource.AddonBattleTalk, started.Source);
        Assert.Equal(DialogueEventReason.AddonShown, started.Reason);
    }

    [Fact]
    public void Chat_DoesNotStartSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.Chat);

        Assert.Empty(events);

        // Even after frames, no session starts
        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        Assert.Empty(events);
    }

    [Fact]
    public void NoneSource_DoesNotStartSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.None);

        Assert.Empty(events);
    }

    // ---------------------------------------------------------------
    // Duplicate suppression
    // ---------------------------------------------------------------

    [Fact]
    public void AdditionalDialogue_DoesNotRestartSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);
        service.NotifyDialogue(TextSource.AddonBattleTalk);

        Assert.Single(events);
    }

    [Fact]
    public void RepeatedAddonVisibility_DoesNotRestartSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        talk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void BothAddonsVisible_StartsOnce()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        talk.Setup(x => x.IsVisible()).Returns(true);
        battleTalk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();

        Assert.Single(events);
    }

    // ---------------------------------------------------------------
    // Session continuation
    // ---------------------------------------------------------------

    [Fact]
    public void VisibleTalk_KeepsSessionAlive()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);
        talk.Setup(x => x.IsVisible()).Returns(true);

        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void VisibleBattleTalk_KeepsSessionAlive()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);
        battleTalk.Setup(x => x.IsVisible()).Returns(true);

        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void SelectionString_BridgesDialogueScreens()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        // Talk closes, SelectString opens
        talk.Setup(x => x.IsVisible()).Returns(false);
        selectString.Setup(x => x.IsVisible()).Returns(true);

        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void SelectionIconString_BridgesDialogueScreens()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        talk.Setup(x => x.IsVisible()).Returns(false);
        selectIcon.Setup(x => x.IsVisible()).Returns(true);

        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void CutsceneCondition_BridgesLongPause()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        talk.Setup(x => x.IsVisible()).Returns(false);
        condition.Setup(c => c[ConditionFlag.WatchingCutscene]).Returns(true);

        for (var i = 0; i < 100; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void Cutscene78Condition_BridgesLongPause()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        talk.Setup(x => x.IsVisible()).Returns(false);
        condition.Setup(c => c[ConditionFlag.WatchingCutscene78]).Returns(true);

        for (var i = 0; i < 100; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void QuestEventCondition_BridgesLongPause()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        talk.Setup(x => x.IsVisible()).Returns(false);
        condition.Setup(c => c[ConditionFlag.OccupiedInQuestEvent]).Returns(true);

        for (var i = 0; i < 100; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void BroadCondition_DoesNotStartSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        condition.Setup(c => c[ConditionFlag.WatchingCutscene]).Returns(true);

        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Empty(events);
    }

    // ---------------------------------------------------------------
    // Session end
    // ---------------------------------------------------------------

    [Fact]
    public void SessionEnds_AfterContinuationSignalsClear()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        Assert.Equal(2, events.Count);
        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);
        Assert.Equal(DialogueEventReason.DialogueContextEnded, ended.Reason);
    }

    [Fact]
    public void TransientGap_DoesNotEndSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        // Two frames without context (below the threshold of 3)
        AdvanceFrame();
        AdvanceFrame();

        // Context returns
        talk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();

        // Stay alive for many more frames
        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void ReturningContext_CancelsPendingEnd()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        // Two frames without context (below the threshold)
        AdvanceFrame();
        AdvanceFrame();

        // Selection dialog opens, resetting the counter
        selectString.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();

        // Keep alive for many more frames
        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Single(events);
    }

    [Fact]
    public void EndEvent_FiresOnlyOnce()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        // Many more inactive frames
        for (var i = 0; i < 10; i++)
            AdvanceFrame();

        Assert.Equal(2, events.Count);
    }

    // ---------------------------------------------------------------
    // Hard boundaries
    // ---------------------------------------------------------------

    [Fact]
    public void TerritoryChange_ForceEndsSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);
        clientState.Raise(c => c.TerritoryChanged += null, 0u);

        Assert.Equal(2, events.Count);
        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);
        Assert.Equal(DialogueEventReason.TerritoryChanged, ended.Reason);
    }

    [Fact]
    public void Logout_ForceEndsSession()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);
        clientState.Raise(c => c.Logout += null, 0, 0);

        Assert.Equal(2, events.Count);
        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);
        Assert.Equal(DialogueEventReason.LoggedOut, ended.Reason);
    }

    [Fact]
    public void Dispose_EmitsEndEvent()
    {
        var collected = new List<NpcDialogueSessionEvent>();
        var service = CreateService();
        using var sub = service.OnEvent.Subscribe(collected.Add);
        service.NotifyDialogue(TextSource.AddonTalk);
        service.Dispose();

        Assert.Equal(2, collected.Count);
        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(collected[1]);
        Assert.Equal(DialogueEventReason.PluginStopped, ended.Reason);
    }

    [Fact]
    public void TerritoryChange_WhileInactive_EmitsNothing()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        clientState.Raise(c => c.TerritoryChanged += null, 0u);

        Assert.Empty(events);
    }

    [Fact]
    public void Logout_WhileInactive_EmitsNothing()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        clientState.Raise(c => c.Logout += null, 0, 0);

        Assert.Empty(events);
    }

    [Fact]
    public void Dispose_WhileInactive_EmitsNothing()
    {
        var collected = new List<NpcDialogueSessionEvent>();
        var service = CreateService();
        using var sub = service.OnEvent.Subscribe(collected.Add);
        service.Dispose();

        Assert.Empty(collected);
    }

    // ---------------------------------------------------------------
    // Event payloads
    // ---------------------------------------------------------------

    [Fact]
    public void StartEvent_HasExpectedShape()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);

        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(IpcEventType.NpcDialogueSessionStarted, started.EventType);
        Assert.Equal(TextSource.AddonTalk, started.Source);
        Assert.Equal(DialogueEventReason.TextReceived, started.Reason);
        Assert.NotEqual(Guid.Empty, started.SessionId);
    }

    [Fact]
    public void EndEvent_HasExpectedShape()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonTalk);
        var sessionId = ((NpcDialogueSessionStartedEvent)events[0]).SessionId;

        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);
        Assert.Equal(IpcEventType.NpcDialogueSessionEnded, ended.EventType);
        Assert.Equal(TextSource.AddonTalk, ended.Source);
        Assert.Equal(DialogueEventReason.DialogueContextEnded, ended.Reason);
        Assert.Equal(sessionId, ended.SessionId);
    }

    [Fact]
    public void BattleTalkSession_PreservesSourceThroughEnd()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        service.NotifyDialogue(TextSource.AddonBattleTalk);
        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(TextSource.AddonBattleTalk, started.Source);

        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);
        Assert.Equal(TextSource.AddonBattleTalk, ended.Source);
        Assert.Equal(started.SessionId, ended.SessionId);
    }

    [Fact]
    public void BattleTalkVisibility_PreservesSourceThroughEnd()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        battleTalk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();
        var started = Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.Equal(TextSource.AddonBattleTalk, started.Source);

        battleTalk.Setup(x => x.IsVisible()).Returns(false);
        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        var ended = Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);
        Assert.Equal(TextSource.AddonBattleTalk, ended.Source);
    }

    // ---------------------------------------------------------------
    // Acceptance test
    // ---------------------------------------------------------------

    [Fact]
    public void FullCutsceneConversation()
    {
        using var service = CreateService();
        using var events = service.OnEvent.ToLiveList();

        // NPC line starts session
        service.NotifyDialogue(TextSource.AddonTalk);
        Assert.Single(events);
        Assert.Equal(TextSource.AddonTalk, ((NpcDialogueSessionStartedEvent)events[0]).Source);

        // Talk closes but cutscene remains active for many frames
        talk.Setup(x => x.IsVisible()).Returns(false);
        condition.Setup(c => c[ConditionFlag.WatchingCutscene]).Returns(true);
        for (var i = 0; i < 100; i++)
            AdvanceFrame();
        Assert.Single(events);

        // Talk reopens, another line arrives
        talk.Setup(x => x.IsVisible()).Returns(true);
        AdvanceFrame();
        service.NotifyDialogue(TextSource.AddonTalk);
        Assert.Single(events);

        // Cutscene ends, all UI closes
        talk.Setup(x => x.IsVisible()).Returns(false);
        condition.Setup(c => c[ConditionFlag.WatchingCutscene]).Returns(false);
        AdvanceFrame();
        AdvanceFrame();
        AdvanceFrame();

        Assert.Equal(2, events.Count);
        Assert.IsType<NpcDialogueSessionStartedEvent>(events[0]);
        Assert.IsType<NpcDialogueSessionEndedEvent>(events[1]);

        // Same session ID across start and end
        var started = (NpcDialogueSessionStartedEvent)events[0];
        var ended = (NpcDialogueSessionEndedEvent)events[1];
        Assert.Equal(started.SessionId, ended.SessionId);
        Assert.Equal(started.Source, ended.Source);
    }
}