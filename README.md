# TextToTalk
Chat TTS plugin for Dalamud. Has support for triggers/exclusions, and WebSocket support for external interfacing.

## Commands
* `/tttconfig`: Opens the configuration window.
* `/canceltts`: Cancel all queued TTS messages.
* `/toggletts`: Turns TTS on or off.

## WebSocket Interfacing
TextToTalk can optionally open a WebSocket server to serve messages over. There are currently two JSON-format messages that can be sent:

TTS prompt:
```
{
	"Type": "Say",
	"Payload": "Someone someone says something"
}
```

TTS cancel:
```
{
	"Type": "Cancel",
	"Payload": ""
}
```

The WebSocket address is shown under the configuration checkbox.

## Screenshots
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/0.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/1.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/TextToTalk/master/Assets/2.png)