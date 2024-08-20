using System.Text.Json.Serialization;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiErrorResponse
{
    [JsonPropertyName("error")] public Inner? Error { get; init; }

    public class Inner
    {
        [JsonPropertyName("message")] public string? Message { get; init; }

        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("param")] public string? Param { get; init; }

        [JsonPropertyName("code")] public string? Code { get; init; }
    }
}