using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using R3;
using System;
using TextToTalk.Events;
using TextToTalk.Talk;

namespace TextToTalk.Services;

// Sessions are intentionally conservative.
//
// Dialogue-specific signals start sessions.
// Broader game state may only extend them.
//
// This avoids false positives from unrelated quest/cutscene state.

public enum DialogueSessionState
{
    Inactive,
    Active,
}

public class DialogueSessionService : IDisposable
{
    private const int EndDelayFrames = 3;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IAddonSelectStringManager addonSelectStringManager;
    private readonly IAddonSelectIconStringManager addonSelectIconStringManager;
    private readonly IAddonTalkManager addonTalkManager;
    private readonly IAddonBattleTalkManager addonBattleTalkManager;

    private DialogueSessionState state = DialogueSessionState.Inactive;
    private int inactiveFrames;
    private Guid sessionId;
    private TextSource sessionSource = TextSource.None;

    private bool prevTalkVisible;
    private bool prevBattleTalkVisible;

    private readonly Subject<NpcDialogueSessionEvent> onEvent = new();

    public Observable<NpcDialogueSessionEvent> OnEvent => onEvent;

    public DialogueSessionService(IFramework framework, IClientState clientState, ICondition condition,
        IAddonSelectStringManager addonSelectStringManager, IAddonSelectIconStringManager addonSelectIconStringManager,
        IAddonTalkManager addonTalkManager, IAddonBattleTalkManager addonBattleTalkManager)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.condition = condition;
        this.addonSelectStringManager = addonSelectStringManager;
        this.addonSelectIconStringManager = addonSelectIconStringManager;
        this.addonTalkManager = addonTalkManager;
        this.addonBattleTalkManager = addonBattleTalkManager;

        this.framework.Update += OnFrameworkUpdate;
        this.clientState.TerritoryChanged += OnTerritoryChanged;
        this.clientState.Logout += OnLogout;
    }

    public void NotifyDialogue(TextSource source)
    {
        if (source is not (TextSource.AddonTalk or TextSource.AddonBattleTalk))
            return;

        this.framework.Run(() =>
        {
            if (state == DialogueSessionState.Inactive)
            {
                StartSession(source, DialogueEventReason.TextReceived);
            }

            ResetInactiveFrames();
        });
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var talkVisible = addonTalkManager.IsVisible();
        var battleTalkVisible = addonBattleTalkManager.IsVisible();

        if (talkVisible && !prevTalkVisible)
            OnDialogueAddonShown(TextSource.AddonTalk);
        if (battleTalkVisible && !prevBattleTalkVisible)
            OnDialogueAddonShown(TextSource.AddonBattleTalk);

        prevTalkVisible = talkVisible;
        prevBattleTalkVisible = battleTalkVisible;

        if (state == DialogueSessionState.Inactive)
            return;

        if (HasContinuationContext(talkVisible, battleTalkVisible))
        {
            ResetInactiveFrames();
            return;
        }

        if (++inactiveFrames >= EndDelayFrames)
        {
            EndSession(DialogueEventReason.DialogueContextEnded);
        }
    }

    private bool HasContinuationContext(bool talkVisible, bool battleTalkVisible)
    {
        return talkVisible ||
               battleTalkVisible ||
               addonSelectStringManager.IsVisible() ||
               addonSelectIconStringManager.IsVisible() ||
               condition[ConditionFlag.WatchingCutscene] ||
               condition[ConditionFlag.WatchingCutscene78] ||
               condition[ConditionFlag.OccupiedInQuestEvent];
    }

    private void OnDialogueAddonShown(TextSource source)
    {
        if (state == DialogueSessionState.Inactive)
        {
            StartSession(source, DialogueEventReason.AddonShown);
        }

        ResetInactiveFrames();
    }

    private void StartSession(TextSource source, DialogueEventReason reason)
    {
        state = DialogueSessionState.Active;
        sessionId = Guid.NewGuid();
        sessionSource = source;
        inactiveFrames = 0;

        onEvent.OnNext(new NpcDialogueSessionStartedEvent(source)
        {
            SessionId = sessionId,
            Reason = reason,
        });
    }

    private void ResetInactiveFrames()
    {
        inactiveFrames = 0;
    }

    private void EndSession(DialogueEventReason reason)
    {
        if (state == DialogueSessionState.Inactive)
            return;

        onEvent.OnNext(new NpcDialogueSessionEndedEvent(sessionSource)
        {
            SessionId = sessionId,
            Reason = reason,
        });

        state = DialogueSessionState.Inactive;
        sessionId = Guid.Empty;
        sessionSource = TextSource.None;
        inactiveFrames = 0;
    }

    private void OnTerritoryChanged(uint _) => EndSession(DialogueEventReason.TerritoryChanged);

    private void OnLogout(int type, int code) => EndSession(DialogueEventReason.LoggedOut);

    public void Dispose()
    {
        EndSession(DialogueEventReason.PluginStopped);

        this.clientState.Logout -= OnLogout;
        this.clientState.TerritoryChanged -= OnTerritoryChanged;
        this.framework.Update -= OnFrameworkUpdate;

        onEvent.Dispose();
    }
}