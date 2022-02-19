[![Download count](https://img.shields.io/endpoint?url=https://vz32sgcoal.execute-api.us-east-1.amazonaws.com/TextToTalk)](https://github.com/karashiiro/TextToTalk)

# TextToTalk
Chat TTS plugin for [Dalamud](https://github.com/goatcorp/Dalamud). Has support for triggers/exclusions, Amazon Polly, and external interfacing over WebSocket.

## Commands
* `/tttconfig`: Opens the configuration window.
* `/canceltts`: Cancel all queued TTS messages.
* `/toggletts`: Turns TTS on or off.
* `/disabletts`: Turns TTS off.
* `/enabletts`: Turns TTS on.

## Lexicons
TextToTalk supports custom lexicons to modify how words are pronounced. For more information, please join our [community lexicons discussion](https://github.com/karashiiro/TextToTalk/discussions/62).

Direct links to information will be added here eventually.

## WebSocket Interfacing
TextToTalk can optionally open a WebSocket server to serve messages over. There are currently two JSON-format messages that can be sent:

TTS prompt:
```
{
	"Type": "Say",
	"Payload": "Firstname Lastname says something",
	"SpeakerGender": 1,
	"Source": "Chat"
}
```

TTS cancel:
```
{
	"Type": "Cancel",
	"Payload": "",
	"SpeakerGender": -1,
	"Source": "None"
}
```

The WebSocket address is shown under the configuration checkbox. Gender codes can be found in [`Gender.cs`](https://github.com/karashiiro/TextToTalk/blob/master/TextToTalk/GameEnums/Gender.cs).

## Screenshots
![image](https://user-images.githubusercontent.com/49822414/126075774-a97d7a11-98c6-40e4-9937-711a8da96926.png)
![image](https://user-images.githubusercontent.com/49822414/126075784-1af622f3-df16-4e00-8de5-bf11f6acb017.png)
![image](https://user-images.githubusercontent.com/49822414/126075793-8b4587e0-1863-44ca-a13f-27a1fcd336d6.png)
![image](https://user-images.githubusercontent.com/49822414/126075802-c694a821-82da-4d87-bf97-06fba9d1e5e4.png)
![image](https://user-images.githubusercontent.com/49822414/126075852-f2aee169-c83c-4916-aca2-e715951eab36.png)
![image](https://user-images.githubusercontent.com/49822414/126075869-480cacb2-8537-41da-aaba-553da5ec9abb.png)
![image](https://user-images.githubusercontent.com/49822414/126075873-aa329726-92eb-4ea1-9127-39810016596b.png)
