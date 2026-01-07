# Architecture Overview

TextToTalk is a Dalamud plugin that enables TTS in FFXIV. This document provides an overview of the project structure.

## Solution Structure

The solution (`src/TextToTalk.sln`) contains multiple projects:

| Project | Description |
|---------|-------------|
| `TextToTalk` | Main plugin - entry point, backends, UI, text processing |
| `TextToTalk.Data` | LiteDB data access layer for persistent storage |
| `TextToTalk.Lexicons` | Pronunciation lexicon parsing (YAML + PLS format) |
| `TextToTalk.UI.Core` | Shared ImGui UI components |
| `TextToTalk.UI.SourceGeneration` | Roslyn source generators for UI data binding |
| `TextToTalk.UngenderedOverrides` | Gender-neutral voice handling |
| `VoiceUnlocker` | Utility to unlock additional Windows TTS voices |
| `websocket-sharp` | WebSocket server (git submodule) |

### Test Projects

| Project | Description |
|---------|-------------|
| `TextToTalk.Tests` | Main plugin tests |
| `TextToTalk.Lexicons.Tests` | Lexicon system tests |
| `TextToTalk.UI.SourceGeneration.Tests` | Source generator tests |
| `TextToTalk.UngenderedOverrides.Tests` | Voice override tests |

## Main Plugin Structure

The main plugin (`src/TextToTalk/`) is organized into these key areas:

- `Backends/` - TTS provider implementations (System, Azure, Polly, etc.)
- `Talk/` - Game addon interaction for NPC dialogue
- `TextProviders/` - Chat and dialogue message handlers
- `Lexicons/` - Custom pronunciation system integration
- `Services/` - Player/NPC info caching
- `Middleware/` - Event processing pipeline
- `UI/` - ImGui configuration windows
- `Migrations/` - Configuration version upgrades

## Plugin Lifecycle

The plugin entry point is `TextToTalk.cs`, which implements `IDalamudPlugin`. On initialization:

1. Database connections are established (LiteDB)
2. Managers are initialized (voice backend, lexicons)
3. Event handlers are set up (chat, dialogue)
4. UI components are created
5. R3 reactive subscriptions are started

## Further Reading

For detailed information on specific systems, see the [Design Documentation](../design/):

- [Backend System](../design/backend-system.md) - TTS provider architecture
- [Event Flow](../design/event-flow.md) - Text processing pipeline
- [Configuration](../design/configuration.md) - Settings storage
- [Lexicon System](../design/lexicon-system.md) - Custom pronunciations
- [UI Architecture](../design/ui-architecture.md) - ImGui patterns
