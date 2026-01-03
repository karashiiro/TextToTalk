using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using TextToTalk.GameEnums;
using Serilog;

namespace TextToTalk.Backends.OpenAI;

public class OpenAiClient(StreamSoundQueue soundQueue, HttpClient http)
{
    private const string UrlBase = "https://api.openai.com";
    
    public record ModelConfig(string ModelName, IReadOnlySet<string> Voices, bool InstructionsSupported, bool SpeedSupported);

    public static readonly List<ModelConfig> Models =
    [
        // Note: while speed is 'technically' supported by gpt-4o-mini-tts, it doesn't appear to influence the output.
        new("gpt-4o-mini-tts", new HashSet<string>
        {
            "alloy",
            "ash",
            "ballad",
            "coral",
            "echo",
            "fable",
            "onyx",
            "nova",
            "sage",
            "shimmer",
            "verse"
        }, true, false),
        new("tts-1", new HashSet<string>
        {
            "nova",
            "shimmer",
            "echo",
            "onyx",
            "fable",
            "alloy",
            "ash",
            "sage",
            "coral"
        }, false, true),
        new("tts-1-hd", new HashSet<string>
        {
            "nova",
            "shimmer",
            "echo",
            "onyx",
            "fable",
            "alloy",
            "ash",
            "sage",
            "coral"
        }, false, false),
    ];
    
    public string? ApiKey { get; set; }

    private void AddAuthorization(HttpRequestMessage req)
    {
        req.Headers.Add("Authorization", $"Bearer {ApiKey}");
    }

    private bool IsAuthorizationSet()
    {
        return ApiKey is { Length: > 0 };
    }

    public async Task TestCredentials()
    {
        if (!IsAuthorizationSet())
        {
            throw new OpenAiMissingCredentialsException("No OpenAI authorization keys have been configured.");
        }

        var uriBuilder = new UriBuilder(UrlBase) { Path = "/v1/models" };
        using var req = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        AddAuthorization(req);

        var res = await http.SendAsync(req);
        await EnsureSuccessStatusCode(res);
    }

    public string? GetInstructionsForRequest(SayRequest request, OpenAiVoicePreset preset)
    {
        var instructionBuilder = new StringBuilder();
        instructionBuilder.AppendLine($"Tone: Final fantasy 14 character named {request.Speaker}");
        if (request.Race is {Length: > 0})
        {
            instructionBuilder.AppendLine($"Race: {request.Race}");
        }

        if (request.BodyType is not BodyType.Unknown)
        {
            instructionBuilder.AppendLine($"BodyType: {request.BodyType}");
        }

        if (preset.Style is {Length: > 0})
        {
            instructionBuilder.AppendLine($"Instructions: {preset.Style}");
        }

        var instructions = instructionBuilder.ToString()
            .Trim();

        return instructions.Length > 0 ? instructions : null;
    }

    public async Task Say(OpenAiVoicePreset preset, SayRequest request, string text, string style)
    {
        if (!IsAuthorizationSet())
        {
            throw new OpenAiMissingCredentialsException("No OpenAI authorization keys have been configured.");
        }
        
        var uriBuilder = new UriBuilder(UrlBase) { Path = "/v1/audio/speech" };
        using var req = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
        AddAuthorization(req);

        string model;
        string voice;
        if (preset.Model != null && Models.Any(m => m.ModelName == preset.Model))
        {
            model = preset.Model;
        }
        else
        {
            model = Models.First().ModelName;
        }
        
        var modelConfig = Models.First(m => m.ModelName == model);
        if (preset.VoiceName != null && modelConfig.Voices.Contains(preset.VoiceName))
        {
            voice = preset.VoiceName;
        }
        else
        {
            voice = modelConfig.Voices.First();
        }

        Dictionary<string, object> args = new()
        {
            ["model"] = model,
            ["input"] = text,
            ["voice"] = voice,
            ["response_format"] = "mp3",
            ["speed"] = modelConfig.SpeedSupported ? preset.PlaybackRate ?? 1.0f : 1.0f
        };

        if (modelConfig.InstructionsSupported)
        {
            string? configinstructions = GetInstructionsForRequest(request, preset);
            if (style != "")
            {
                args["instructions"] = style;
            }
            //// Instructions from style take precedence over preset instructions.
            //else if (configinstructions != null)
            //{
            //    args["instructions"] = configinstructions;
            //}
        }   //
        var json = JsonSerializer.Serialize(args);
        DetailedLog.Verbose(json);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        req.Content = content;

        var res = await http.SendAsync(req);
        await EnsureSuccessStatusCode(res);

        var mp3Stream = new MemoryStream();
        var responseStream = await res.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(mp3Stream);
        mp3Stream.Seek(0, SeekOrigin.Begin);

        soundQueue.EnqueueSound(mp3Stream, request.Source, StreamFormat.Mp3, preset.Volume);
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage res)
    {
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new OpenAiUnauthorizedException(res.StatusCode, "Unauthorized request.");
        }

        if (!res.IsSuccessStatusCode)
        {
            try
            {
                var content = await res.Content.ReadAsStringAsync();
                DetailedLog.Debug(content);

                var error = JsonSerializer.Deserialize<OpenAiErrorResponse>(content);
                if (error?.Error != null)
                {
                    throw new OpenAiFailedException(res.StatusCode, error.Error,
                        $"Request failed with status code {error.Error.Code}: {error.Error.Message}");
                }
            }
            catch (Exception e) when (e is not OpenAiFailedException)
            {
                DetailedLog.Error(e, "Failed to parse OpenAI error response.");
            }

            throw new OpenAiFailedException(res.StatusCode, null, $"Request failed with status code {res.StatusCode}.");
        }
    }
}