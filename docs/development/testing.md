# Testing Guide

This guide covers how to write tests and make your code testable.

## Test Framework

- **xUnit 2.7.0** - Test framework
- **Moq 4.20.70** - Mocking library

## Running Tests

From the `src/` directory:

```bash
dotnet test
```

## Test Projects

| Project | Purpose |
|---------|---------|
| `TextToTalk.Tests` | Main plugin tests (services, backends, utilities) |
| `TextToTalk.Lexicons.Tests` | Lexicon parsing and processing |
| `TextToTalk.UI.SourceGeneration.Tests` | Source generator verification |
| `TextToTalk.UngenderedOverrides.Tests` | Voice override system |

## Making Code Testable

The key to testable code is **separating concerns** and **injecting dependencies**. Here are the patterns we use:

### 1. Dependency Injection

Accept dependencies through the constructor instead of creating them internally:

```csharp
// Testable - dependencies are injected
public class NotificationService(
    INotificationManager notificationManager,
    IChatGui chat,
    IClientState clientState)
{
    // Use injected dependencies
}

// In tests, you can mock these:
var mockNotificationManager = new Mock<INotificationManager>();
var mockChatGui = new Mock<IChatGui>();
var mockClientState = new Mock<IClientState>();

var service = new NotificationService(
    mockNotificationManager.Object,
    mockChatGui.Object,
    mockClientState.Object);
```

### 2. Interface Abstraction

Create interfaces to decouple components:

```csharp
// Interface allows mocking
public interface IWebsocketConfigProvider
{
    int GetPort();
    IPAddress? GetAddress();
}

// Implementation uses the interface
public class WSServer(IWebsocketConfigProvider configProvider)
{
    // ...
}

// Test can provide mock config
var mockConfig = Mock.Of<IWebsocketConfigProvider>();
var server = new WSServer(mockConfig);
```

### 3. Func<T> Delegation

For time-dependent or configurable behavior, accept a function:

```csharp
// Accepts a function to get the rate limit
public class RateLimiter(Func<long> getLimitMs)
{
    public bool TryRateLimit(string speaker)
    {
        var limit = getLimitMs(); // Called at runtime
        // ...
    }
}

// Test with deterministic value
var limiter = new RateLimiter(() => 200); // Always 200ms
```

## Writing Tests

### Basic Tests with [Fact]

Use `[Fact]` for simple unit tests:

```csharp
[Fact]
public void RateLimiter_Works_WhenNotLimited()
{
    using var limiter = new RateLimiter(() => 200);
    Assert.False(limiter.TryRateLimit("speaker"));
}
```

### Parameterized Tests with [Theory]

Use `[Theory]` with `[InlineData]` for multiple test cases:

```csharp
[Theory]
[InlineData("", "")]
[InlineData("H-Hello", "Hello")]
[InlineData("b-but", "but")]
public void RemoveStutters_Works(string input, string expected)
{
    var result = TalkUtils.RemoveStutters(input);
    Assert.Equal(expected, result);
}
```

### Mocking with Moq

```csharp
[Fact]
public void NotifyWarning_SendsWarning()
{
    // Arrange
    var notificationManager = new Mock<INotificationManager>();
    var chatGui = new Mock<IChatGui>();
    var clientState = new Mock<IClientState>();

    // Setup mock behavior
    clientState.Setup(x => x.IsLoggedIn).Returns(true);
    notificationManager
        .Setup(x => x.AddNotification(It.IsAny<Notification>()))
        .Verifiable();

    var service = new NotificationService(
        notificationManager.Object,
        chatGui.Object,
        clientState.Object);

    // Act
    service.NotifyWarning("Title", "Description");
    service.ProcessNotifications();

    // Assert
    notificationManager.Verify();
}
```

### Builder Pattern for Test Data

For complex test objects, use a builder:

```csharp
public class LexiconBuilder
{
    private readonly List<Lexeme> lexemes = new();

    public LexiconBuilder WithLexeme(Lexeme lexeme)
    {
        lexemes.Add(lexeme);
        return this;
    }

    public string Build()
    {
        // Generate XML from lexemes
    }
}

// Usage
var lexicon = new LexiconBuilder()
    .WithLexeme(new Lexeme { Graphemes = ["Bahamut"], Phoneme = "bɑhɑmɪt" })
    .Build();
```

## What Needs Manual Testing

Some areas can't be easily unit tested and require manual validation:

| Area | Reason |
|------|--------|
| ImGui UI rendering | Requires Dalamud ImGui context |
| Game state access | Requires active game connection |
| Audio output | Hardware-dependent (NAudio) |
| Main plugin entry | Requires full Dalamud environment |
| Addon interception | Requires game UI state |

For these areas, test manually before submitting your PR.

## Example Test Files

Study these files for well-structured test examples:

| File | Pattern Demonstrated |
|------|---------------------|
| `TextToTalk.Tests/RateLimiterTests.cs` | Pure logic testing |
| `TextToTalk.Tests/Services/NotificationServiceTests.cs` | Mocking Dalamud services |
| `TextToTalk.Tests/Backends/Websocket/WSServerTests.cs` | Integration tests with real WebSocket |
| `TextToTalk.Tests/Utils/TalkUtilsTests.cs` | Parameterized tests with [Theory] |
| `TextToTalk.Lexicons.Tests/LexiconBuilder.cs` | Builder pattern for test data |

## Tips

1. **Test one thing per test** - Keep tests focused
2. **Use descriptive names** - `Method_Scenario_ExpectedResult`
3. **Arrange-Act-Assert** - Structure tests clearly
4. **Don't test implementation details** - Test behavior, not internals
