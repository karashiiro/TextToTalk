# Contributing

Thank you for your interest in contributing to TextToTalk! This guide will help you get set up and ready to contribute.

## Prerequisites

Before you begin, make sure you have:

- **.NET 10.0 SDK**
- **Dalamud**
- **Git**

### Dalamud Library Paths

The project expects Dalamud to be in one of these locations, depending on your OS:

| OS | Path |
|----|------|
| Windows | `%APPDATA%\XIVLauncher\addon\Hooks\dev\` |
| Linux | `$DALAMUD_HOME/` |
| macOS | `$HOME/Library/Application Support/XIV on Mac/dalamud/Hooks/dev/` |

## Getting the Code

### Fresh Clone

```bash
git clone --recurse-submodules https://github.com/karashiiro/TextToTalk.git
```

### Already Cloned?

If you've already cloned without submodules, initialize them with:

```bash
git submodule update --init --recursive
```

## Building

All commands should be run from the `src/` directory:

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

## Running Tests

```bash
dotnet test
```

## Contributing Changes

1. **Fork** the repository to your own GitHub account
2. **Create a branch** with a descriptive name for your changes
3. **Make your changes**
4. **Before submitting:**
   - Run `dotnet test` and ensure all tests pass
   - If your code isn't easily testable, validate it manually
5. **Submit a Pull Request** targeting the `main` branch

## Reporting Bugs

Found a bug? Please open an issue on [GitHub Issues](https://github.com/karashiiro/TextToTalk/issues) with:

- Steps to reproduce the issue
- Expected vs actual behavior
- Your game version and plugin version
- Which TTS backend you're using

## Learn More

For deeper documentation on the codebase:

- [Development Documentation](docs/development/index.md) - Architecture, testing, and adding new backends
- [Design Documentation](docs/design/) - System architecture and design decisions
- [Lexicon Documentation](lexicons/README.md) - Custom pronunciation system
