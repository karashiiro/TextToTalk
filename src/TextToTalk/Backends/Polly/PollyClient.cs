using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Polly
{
    public class PollyClient : IDisposable
    {
        private readonly AmazonPollyClient client;
        private readonly StreamSoundQueue soundQueue;
        private readonly LexiconManager lexiconManager;

        public PollyClient(string accessKey, string secretKey, RegionEndpoint region, LexiconManager lexiconManager)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            this.client = new AmazonPollyClient(credentials, region);
            this.soundQueue = new StreamSoundQueue();
            this.lexiconManager = lexiconManager;
        }

        public IList<Voice> GetVoicesForEngine(Engine engine)
        {
            var voicesReq = new DescribeVoicesRequest
            {
                Engine = engine,
            };

            var voices = new List<Voice>();
            string nextToken;
            do
            {
                var voicesRes = this.client.DescribeVoicesAsync(voicesReq).GetAwaiter().GetResult();
                voices.AddRange(voicesRes.Voices);
                nextToken = voicesRes.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));
            
            return voices;
        }

        public TextSource GetCurrentlySpokenTextSource()
        {
            // This should probably be designed differently.
            return this.soundQueue.GetCurrentlySpokenTextSource();
        }

        public async Task Say(Engine engine, VoiceId voice, int sampleRate, int playbackRate, float volume, TextSource source, string text)
        {
            var ssml = this.lexiconManager.MakeSsml(text, playbackRate: playbackRate, includeSpeakAttributes: false);
            PluginLog.Log(ssml);

            var req = new SynthesizeSpeechRequest
            {
                Text = ssml,
                VoiceId = voice,
                Engine = engine,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = sampleRate.ToString(),
                TextType = TextType.Ssml,
            };

            SynthesizeSpeechResponse res;
            try
            {
                res = await this.client.SynthesizeSpeechAsync(req);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Synthesis request failed in {0}.", nameof(PollyClient));
                return;
            }

            var responseStream = new MemoryStream();
            await res.AudioStream.CopyToAsync(responseStream);
            responseStream.Seek(0, SeekOrigin.Begin);

            this.soundQueue.EnqueueSound(responseStream, source, StreamFormat.Mp3, volume);
        }

        public Task CancelAllSounds()
        {
            this.soundQueue.CancelAllSounds();
            return Task.CompletedTask;
        }

        public Task CancelFromSource(TextSource source)
        {
            this.soundQueue.CancelFromSource(source);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.client.Dispose();
            this.soundQueue.Dispose();
        }
    }
}