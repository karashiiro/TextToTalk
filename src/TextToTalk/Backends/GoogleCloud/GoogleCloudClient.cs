using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.TextToSpeech.V1;
using WebSocketSharp;

namespace TextToTalk.Backends.GoogleCloud;

public class GoogleCloudClient
{
    private TextToSpeechClient? client;
    private readonly StreamSoundQueue? soundQueue;
    public List<string>? Voices;
    public List<string>? Locales;

    public GoogleCloudClient(StreamSoundQueue soundQueue, string pathToCredential)
    {
        this.soundQueue = soundQueue;
        if (pathToCredential.IsNullOrEmpty()) return;
        Init(pathToCredential);
    }

    public void Init(string pathToCredential)
    {
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", pathToCredential);
        this.client = TextToSpeechClient.Create();
        this.Voices = GetGoogleTextToSpeechVoices();
        this.Locales = ExtractUniqueLocales(Voices);
    }

    public List<string>? GetGoogleTextToSpeechVoices()
    {
        if (client == null) return new List<string>();
        var response = client.ListVoices("");

        var fetchedVoices = new List<string>();
        foreach (var voice in response.Voices)
        {
            fetchedVoices.Add(voice.Name);
        }

        return fetchedVoices;
    }

    public List<string> ExtractUniqueLocales(List<string>? voicesList)
    {
        if (voicesList == null) return new List<string>();
        HashSet<string> uniqueLocales = new HashSet<string>();
        foreach (string language in voicesList)
        {
            var segments = language.Split('-');
            if (segments.Length >= 2)
            {
                string locale = string.Join("-", segments.Take(2));
                uniqueLocales.Add(locale);
            } 
        }
        return uniqueLocales.ToList().OrderBy(lang => lang).ToList();
    }

    public async Task Say(string? locale, string? voice, float? speed, float volume, TextSource source,
        string text)
    {
        if (client == null || soundQueue == null || locale == null)
        {
            return;
        }

        var request = new SynthesizeSpeechRequest
        {
            Input = new SynthesisInput { Text = text },
            Voice = new VoiceSelectionParams
            {
                LanguageCode = locale,
                Name = voice ?? "en-US-Wavenet-A"
            },
            AudioConfig = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Mp3,
                SpeakingRate = speed ?? 1.0f,
                VolumeGainDb = volume
            }
        };

        var response = await client.SynthesizeSpeechAsync(request);

        MemoryStream mp3Stream = new MemoryStream(response.AudioContent.ToByteArray());
        mp3Stream.Seek(0, SeekOrigin.Begin);

        soundQueue.EnqueueSound(mp3Stream, source, StreamFormat.Mp3, volume);
    }
}