[![Download count](https://img.shields.io/endpoint?url=https://qzysathwfhebdai6xgauhz4q7m0mzmrf.lambda-url.us-east-1.on.aws/TextToTalk)](https://github.com/karashiiro/TextToTalk)

# TextToTalk
Chat TTS plugin for [Dalamud](https://github.com/goatcorp/Dalamud). Has support for triggers/exclusions, several TTS providers, and more!

## Commands
* `/tttconfig`: Opens the configuration window.
* `/canceltts`: Cancel all queued TTS messages.
* `/toggletts`: Turns TTS on or off.
* `/disabletts`: Turns TTS off.
* `/enabletts`: Turns TTS on.

## Lexicons
TextToTalk supports custom lexicons to modify how words are pronounced. For more information, please join our [community lexicons discussion](https://github.com/karashiiro/TextToTalk/discussions/62).

Direct links to information will be added here eventually.

## Supported TTS providers
* System (Windows)
* AWS Polly
* Azure (Microsoft Cognitive Services)
* Uberduck
* Websocket

## WebSocket interfacing
TextToTalk can optionally open a WebSocket server to serve messages over.
There are currently two JSON-format messages that can be sent (see
[`IpcMessage`](https://github.com/karashiiro/TextToTalk/blob/main/src/TextToTalk/Backends/Websocket/IpcMessage.cs)):

TTS prompt:
```json5
{
  "Type": "Say",
  "Payload": "Firstname Lastname says something",
  // Will replace the logged-in player's name with {{FULL_NAME}}, {{FIRST_NAME}}, or {{LAST_NAME}} as appropriate.
  "PayloadTemplate": "Firstname Lastname says something",
  "Voice": {
    "Name": "Gender"
  },
  "Speaker": "Firstname Lastname",
  // or "AddonTalk", or "AddonBattleTalk"
  "Source": "Chat",
  "StuttersRemoved": false,
  // or null, for non-NPCs
  "NpcId": 1000115,
  // Refer to https://dalamud.dev/api/Dalamud.Game.Text/Enums/XivChatType
  "ChatType": 10,
  // Refer to https://dalamud.dev/api/Dalamud/Enums/ClientLanguage
  "Language": "English"
}
```

TTS cancel:
```json5
{
  "Type": "Cancel",
  "Payload": "",
  "PayloadTemplate": "",
  "Voice": null,
  "Speaker": null,
  // or "Chat", "AddonTalk", or "AddonBattleTalk"
  "Source": "None",
  "StuttersRemoved": false,
  "NpcId": null,
  "ChatType": null,
  "Language": null
}
```

## Screenshots
![image](https://user-images.githubusercontent.com/49822414/126075774-a97d7a11-98c6-40e4-9937-711a8da96926.png)
![image](https://user-images.githubusercontent.com/49822414/126075784-1af622f3-df16-4e00-8de5-bf11f6acb017.png)
![image](https://user-images.githubusercontent.com/49822414/126075793-8b4587e0-1863-44ca-a13f-27a1fcd336d6.png)
![image](https://user-images.githubusercontent.com/49822414/126075802-c694a821-82da-4d87-bf97-06fba9d1e5e4.png)
![image](https://user-images.githubusercontent.com/49822414/126075852-f2aee169-c83c-4916-aca2-e715951eab36.png)
![image](https://user-images.githubusercontent.com/49822414/126075869-480cacb2-8537-41da-aaba-553da5ec9abb.png)
![image](https://user-images.githubusercontent.com/49822414/126075873-aa329726-92eb-4ea1-9127-39810016596b.png)

## Development
Refer to the [wiki](https://github.com/karashiiro/TextToTalk/wiki/Development) for dev documentation.
