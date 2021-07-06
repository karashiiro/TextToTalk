# TextToTalk
Chat TTS plugin for [Dalamud](https://github.com/goatcorp/Dalamud). Has support for triggers/exclusions, Amazon Polly, and external interfacing over WebSocket.

## Commands
* `/tttconfig`: Opens the configuration window.
* `/canceltts`: Cancel all queued TTS messages.
* `/toggletts`: Turns TTS on or off.
* `/disabletts`: Turns TTS off.
* `/enabletts`: Turns TTS on.

## WebSocket Interfacing
TextToTalk can optionally open a WebSocket server to serve messages over. There are currently two JSON-format messages that can be sent:

TTS prompt:
```
{
	"Type": "Say",
	"Payload": "Someone someone says something",
	"SpeakerGender": 1
}
```

TTS cancel:
```
{
	"Type": "Cancel",
	"Payload": "",
	"SpeakerGender": -1
}
```

The WebSocket address is shown under the configuration checkbox. Gender codes can be found in [`Gender.cs`](https://github.com/karashiiro/TextToTalk/blob/master/TextToTalk/GameEnums/Gender.cs).

## Screenshots
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/0.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/1.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/2.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/3.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/4.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/5.png)
