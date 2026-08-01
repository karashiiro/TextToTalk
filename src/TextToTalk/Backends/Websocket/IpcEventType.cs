using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TextToTalk.Backends.Websocket;

[JsonConverter(typeof(StringEnumConverter))]
public enum IpcEventType
{
    NpcDialogueSessionStarted,
    NpcDialogueSessionEnded,
}