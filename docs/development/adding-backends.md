# Adding TTS Backends

This guide walks you through adding a new TTS provider to TextToTalk.

## Overview

TextToTalk uses a Strategy pattern for TTS backends. All backends inherit from `VoiceBackend` and are managed by `VoiceBackendManager`, which holds exactly one active backend at a time.

## Steps to Add a Backend

### 1. Create the Backend Class

Create a new class in `src/TextToTalk/Backends/YourBackend/` that inherits from `VoiceBackend`:

```csharp
public class YourBackend : VoiceBackend
{
    public override void Say(SayRequest request)
    {
        // Synthesize and play TTS audio
    }

    public override void CancelAllSpeech()
    {
        // Stop all playback
    }

    public override void CancelSay(TextSource source)
    {
        // Cancel speech from a specific source (Chat, AddonTalk, etc.)
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        // Render ImGui configuration UI
    }

    public override TextSource GetCurrentlySpokenTextSource()
    {
        // Return the source of currently playing speech
    }
}
```

### 2. Create a Voice Preset Class

Each backend needs its own preset type since voice configuration varies. Create a class inheriting from `VoicePreset`:

```csharp
public class YourVoicePreset : VoicePreset
{
    // Backend-specific voice settings
    public string VoiceId { get; set; }
    public float Speed { get; set; } = 1.0f;
    // etc.
}
```

### 3. Add the Backend Enum Value

Add your backend to the `TTSBackend` enum in `src/TextToTalk/Backends/TTSBackend.cs`:

```csharp
public enum TTSBackend
{
    // ... existing backends ...
    YourBackend,
}
```

### 4. Add Extension Method Cases

In the same file (`TTSBackend.cs`), add cases to the extension methods:

**GetFormattedName** - Display name shown in the UI:
```csharp
TTSBackend.YourBackend => "Your Backend Name",
```

**AreLexiconsEnabled** - Whether custom pronunciations work with this backend:
```csharp
TTSBackend.YourBackend => true, // or false if lexicons aren't supported
```

### 5. Register in VoiceBackendManager

Add a case to `VoiceBackendManager.CreateBackendFor()`:

```csharp
case TTSBackend.YourBackend:
    return new YourBackend(/* dependencies */);
```

### 6. Add Preset Deserialization

Add a case to `VoicePresetConfiguration.RepairPreset()` to handle loading your preset from JSON:

```csharp
TTSBackend.YourBackend => new YourVoicePreset
{
    Id = Convert.ToInt32(GetNullableValue<long?>(corrupted, "Id")),
    Name = GetNullableValue<string?>(corrupted, "Name"),
    // ... your other properties ...
    EnabledBackend = TTSBackend.YourBackend,
},
```

This is required for saved voice presets to load correctly.

## Backend Architecture Pattern

For cloud/API backends, we tend to follow a four-component pattern that separates concerns:

```
YourBackend/
├── YourBackend.cs         # Main VoiceBackend class
├── YourBackendUI.cs       # ImGui rendering
├── YourBackendUIModel.cs  # UI state and business logic
├── YourClient.cs          # API communication
└── YourVoicePreset.cs     # Voice configuration
```

### Component Responsibilities

**Backend** (`YourBackend.cs`)
- Inherits from `VoiceBackend`
- Creates and owns the UI and UIModel
- Delegates TTS operations to the Client (through UIModel)
- Delegates settings rendering to BackendUI

```csharp
public class YourBackend : VoiceBackend
{
    private readonly YourBackendUI ui;
    private readonly YourBackendUIModel uiModel;

    public YourBackend(PluginConfiguration config, HttpClient http)
    {
        this.uiModel = new YourBackendUIModel(config);
        this.ui = new YourBackendUI(this.uiModel, config, http, this);
    }

    public override void Say(SayRequest request)
    {
        // Delegate to client through uiModel
        this.uiModel.Client?.Say(...);
    }

    public override void DrawSettings(IConfigUIDelegates helpers)
    {
        this.ui.DrawSettings(helpers);
    }
}
```

**BackendUI** (`YourBackendUI.cs`)
- Contains all ImGui rendering code
- Reads state from the UIModel
- Calls UIModel methods when user interacts with controls
- No direct TTS logic

```csharp
public class YourBackendUI
{
    private readonly YourBackendUIModel model;

    public void DrawSettings(IConfigUIDelegates helpers)
    {
        // Read from model
        var region = this.model.GetCurrentRegion();

        // ImGui controls
        if (ImGui.Combo("Region", ref regionIndex, ...))
        {
            // Update through model
            this.model.SetCurrentRegion(...);
        }
    }
}
```

**BackendUIModel** (`YourBackendUIModel.cs`)
- Holds UI state and configuration
- Creates and owns the Client
- Provides getters/setters for UI-bound state
- Handles authentication/login logic
- Stores error state for UI display

```csharp
public class YourBackendUIModel : IDisposable
{
    public YourClient? Client { get; private set; }
    public Exception? LoginException { get; private set; }

    public void LoginWith(string apiKey)
    {
        try
        {
            Client = new YourClient(apiKey, ...);
            LoginException = null;
        }
        catch (Exception e)
        {
            LoginException = e;
        }
    }
}
```

**Client** (`YourClient.cs`)
- Wraps the TTS SDK/API
- Handles synthesis requests
- Manages the sound queue
- Pure API/audio operations, no UI concerns

```csharp
public class YourClient : IDisposable
{
    private readonly StreamSoundQueue soundQueue;

    public async Task Say(string text, float volume, TextSource source)
    {
        // Call API, get audio stream
        var audioStream = await CallApi(text);

        // Enqueue for playback
        this.soundQueue.EnqueueSound(audioStream, source, StreamFormat.Mp3, volume);
    }
}
```

### Why This Pattern?

- **Testability** - UIModel can be tested without ImGui
- **Separation of Concerns** - Each class has one job
- **Maintainability** - UI changes don't affect API logic and vice versa
- **Error Handling** - Login errors stored in UIModel, displayed by UI
- **Async Data Loading** - UIModel handles async operations (fetching voices, login) and stores results for the UI to read, since ImGui's immediate mode doesn't mix well with async code

## Reference Implementations

Study these existing backends as templates:

- **PollyBackend**, **AzureBackend** - Full pattern examples (Backend/UI/UIModel/Client)
- **SystemBackend** - Simpler local TTS without separate Client class
- **WebsocketBackend** - External TTS delegation, minimal UI

## Audio Playback

Backends should push audio to a queue rather than playing directly. This:
- Prevents blocking during synthesis
- Enables cancellation
- Maintains message ordering
- Tracks audio source for selective cancellation

See `SoundHandler` for the audio queue implementation.

## Testing Your Backend

1. Build and load the plugin
2. Open `/tttconfig` and select your backend
3. Test with various chat messages and NPC dialogue
4. Verify cancellation works correctly

## Further Reading

See [Backend System Design](../design/backend-system.md) for detailed architecture information.
