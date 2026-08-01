namespace TextToTalk.Events;

public enum DialogueEventReason
{
    TextReceived,
    AddonShown,
    DialogueContextEnded,
    TerritoryChanged,
    LoggedOut,
    PluginStopped,
}